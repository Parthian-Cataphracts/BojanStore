using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Phase 4 — the money path, rule by rule.
/// </summary>
/// <remarks>
/// Each test names the rule from <c>BACKEND.md</c> Phase 4 it covers. These are
/// the negative cases: the positive path is one test, and the other seven are
/// the ways a basket arriving from the shopper's own browser can lie.
/// </remarks>
public sealed class CheckoutEndpointsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _customerId;
    private Guid _addressId;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 300_000, stock: 5);
            var customer = await TestData.AddCustomerAsync(db, "09121110010");
            var address = await TestData.AddAddressAsync(db, customer.Id);
            await TestData.AddCheckoutMethodsAsync(db);

            _productId = product.Id;
            _customerId = customer.Id;
            _addressId = address.Id;
        });

        _client = _factory.CreateCustomerClient(_customerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    private object OrderBody(int quantity = 2, string payment = "cod", string? coupon = null) => new
    {
        lines = new[] { new { productId = _productId.ToString(), quantity } },
        addressId = _addressId.ToString(),
        shippingMethodId = "standard",
        paymentMethodId = payment,
        couponCode = coupon,
    };

    [Fact]
    public async Task Placing_an_order_prices_it_from_the_database_and_reserves_stock()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", OrderBody());
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("BZ-", body.GetProperty("orderNumber").GetString());

        // Cash on delivery owes no gateway redirect, and the checkout redirects
        // whenever paymentUrl is present.
        Assert.False(body.TryGetProperty("paymentUrl", out _));

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.Include(o => o.Lines).SingleAsync();
            Assert.Equal(600_000, order.Subtotal.Amount);
            Assert.Equal(45_000, order.Shipping.Amount);
            Assert.Equal(645_000, order.Total.Amount);
            Assert.Equal(300_000, order.Lines.Single().UnitPrice.Amount);

            // Rule 2 — reserved, not merely checked.
            Assert.Equal(3, (await db.Products.SingleAsync()).Stock);
        });
    }

    /// <summary>A line naming a SKU prices and reserves from that SKU, not from the parent product.</summary>
    [Fact]
    public async Task A_line_naming_a_sku_prices_and_reserves_from_the_sku()
    {
        Guid skuId = default;
        await _factory.WithDbAsync(async db =>
        {
            var sku = await TestData.AddSkuAsync(db, _productId, "p-01-cream-a5", price: 350_000, stock: 2);
            skuId = sku.Id;
        });

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = new[] { new { productId = _productId.ToString(), quantity = 2, skuId = skuId.ToString() } },
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
        });
        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.Include(o => o.Lines).SingleAsync();
            var line = order.Lines.Single();
            Assert.Equal(skuId, line.SkuId);
            Assert.Equal(350_000, line.UnitPrice.Amount);
            Assert.Equal(700_000, order.Subtotal.Amount);

            // The SKU's own stock is reserved — the product's is untouched.
            Assert.Equal(0, (await db.ProductSkus.SingleAsync()).Stock);
            Assert.Equal(5, (await db.Products.SingleAsync()).Stock);
        });
    }

    /// <summary>A quantity beyond a SKU's own stock is refused even though the product has plenty.</summary>
    [Fact]
    public async Task Ordering_more_than_a_skus_stock_is_refused_even_when_the_product_has_more()
    {
        Guid skuId = default;
        await _factory.WithDbAsync(async db =>
        {
            var sku = await TestData.AddSkuAsync(db, _productId, "p-01-teal-a4", price: 300_000, stock: 1);
            skuId = sku.Id;
        });

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = new[] { new { productId = _productId.ToString(), quantity = 2, skuId = skuId.ToString() } },
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, await db.Orders.CountAsync()));
    }

    [Fact]
    public async Task A_gateway_payment_comes_back_with_a_payment_url()
    {
        var body = await (await _client.PostAsJsonAsync("/api/orders", OrderBody(1, payment: "gateway")))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("paymentUrl", out var url));
        Assert.False(string.IsNullOrWhiteSpace(url.GetString()));
    }

    /// <summary>Rule 2 — a quantity beyond stock is refused, not silently trimmed.</summary>
    [Fact]
    public async Task Ordering_more_than_the_stock_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", OrderBody(quantity: 9));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(5, (await db.Products.SingleAsync()).Stock));
    }

    /// <summary>
    /// Rule 2, by the back door: the same product on two lines is one quantity.
    /// </summary>
    /// <remarks>
    /// Stock is 5. Split across two lines of 3 each line passes a stock test
    /// taken on its own, and the reservation that follows then tries to take 6
    /// — historically a 500 from <c>ReduceStock</c> rather than the conflict a
    /// short basket is owed. The lines are summed before either check, so this
    /// is the same rejection as asking for 6 outright.
    /// </remarks>
    [Fact]
    public async Task Splitting_a_product_across_lines_cannot_exceed_its_stock()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = new[]
            {
                new { productId = _productId.ToString(), quantity = 3 },
                new { productId = _productId.ToString(), quantity = 3 },
            },
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(5, (await db.Products.SingleAsync()).Stock);
            Assert.False(await db.Orders.AnyAsync());
        });
    }

    /// <summary>
    /// The per-line quantity ceiling counts the product, not the line.
    /// </summary>
    /// <remarks>
    /// Twenty is the most of one product an order may carry. Two lines of
    /// twenty satisfy the per-line rule individually while ordering forty, so
    /// the ceiling is applied to the consolidated quantity.
    /// </remarks>
    [Fact]
    public async Task The_quantity_ceiling_applies_to_the_product_not_the_line()
    {
        await _factory.WithDbAsync(async db =>
        {
            var product = await db.Products.SingleAsync();
            product.IncreaseStock(500);
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = new[]
            {
                new { productId = _productId.ToString(), quantity = 20 },
                new { productId = _productId.ToString(), quantity = 20 },
            },
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.False(await db.Orders.AnyAsync()));
    }

    /// <summary>
    /// The coupon endpoint bounds its basket the way the order endpoint does.
    /// </summary>
    /// <remarks>
    /// It has no reason to accept a larger basket than can be ordered, and an
    /// unbounded array here becomes an unbounded <c>IN</c> clause — one request,
    /// so the rate limiter never sees it coming.
    /// </remarks>
    [Fact]
    public async Task The_coupon_endpoint_refuses_a_basket_larger_than_an_order()
    {
        var lines = Enumerable.Range(0, 51)
            .Select(_ => new { productId = _productId.ToString(), quantity = 1 })
            .ToArray();

        var response = await _client.PostAsJsonAsync("/api/cart/coupon", new { code = "WELCOME", lines });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The delivery window the shopper chose reaches the order.
    /// </summary>
    /// <remarks>
    /// Screen 74 asks for a day and a slot. There was nowhere on the order to
    /// put the answer, so it was collected and discarded — an operator packing
    /// the box had no way to know what had been asked for.
    /// </remarks>
    [Fact]
    public async Task The_chosen_delivery_window_is_stored_on_the_order()
    {
        var window = "شنبه ۱۰ مرداد، ۹ تا ۱۳";

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = new[] { new { productId = _productId.ToString(), quantity = 1 } },
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
            deliveryWindow = window,
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Equal(window, (await db.Orders.SingleAsync()).DeliveryWindow));
    }

    /// <summary>An order placed without one is not an error — screen 08 never asks.</summary>
    [Fact]
    public async Task An_order_without_a_delivery_window_stores_none()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", OrderBody(quantity: 1));
        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Null((await db.Orders.SingleAsync()).DeliveryWindow));
    }

    /// <summary>Rule 3 — the address must belong to the caller.</summary>
    [Fact]
    public async Task An_address_belonging_to_someone_else_is_refused()
    {
        Guid strangersAddress = default;

        await _factory.WithDbAsync(async db =>
        {
            var other = await TestData.AddCustomerAsync(db, "09121110011");
            strangersAddress = (await TestData.AddAddressAsync(db, other.Id)).Id;
        });

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = new[] { new { productId = _productId.ToString(), quantity = 1 } },
            addressId = strangersAddress.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Rule 4 — both method ids must exist.</summary>
    [Fact]
    public async Task An_unknown_shipping_method_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = new[] { new { productId = _productId.ToString(), quantity = 1 } },
            addressId = _addressId.ToString(),
            shippingMethodId = "teleport",
            paymentMethodId = "cod",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Rule 6 — an empty basket owes nothing and is refused.</summary>
    [Fact]
    public async Task An_empty_basket_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            lines = Array.Empty<object>(),
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Rule 7 — the worst bug this system can have.</summary>
    [Fact]
    public async Task The_same_idempotency_key_twice_returns_one_order()
    {
        _client.DefaultRequestHeaders.Add("Idempotency-Key", "test-key-0001");

        var first = await _client.PostAsJsonAsync("/api/orders", OrderBody(1));
        var second = await _client.PostAsJsonAsync("/api/orders", OrderBody(1));

        var firstNumber = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNumber").GetString();
        var secondNumber = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNumber").GetString();

        Assert.Equal(firstNumber, secondNumber);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Orders.CountAsync());
            // The second submission must not have taken a second unit either.
            Assert.Equal(4, (await db.Products.SingleAsync()).Stock);
        });
    }

    /// <summary>
    /// The same guarantee without the header, which is what the shipped
    /// checkout sends today — the key is derived from the basket instead.
    /// </summary>
    [Fact]
    public async Task A_repeated_submission_with_no_header_still_places_one_order()
    {
        await _client.PostAsJsonAsync("/api/orders", OrderBody(1));
        await _client.PostAsJsonAsync("/api/orders", OrderBody(1));

        await _factory.WithDbAsync(async db => Assert.Equal(1, await db.Orders.CountAsync()));
    }

    /// <summary>Rule 5 — the coupon is re-applied here, whatever the client believed.</summary>
    [Fact]
    public async Task A_coupon_is_revalidated_at_order_time()
    {
        await _factory.WithDbAsync(async db =>
            await TestData.AddCouponAsync(db, "BOJAN10", amountOff: 120_000, minimumSpend: 500_000));

        // One unit is 300,000 — below the coupon's minimum spend.
        var refused = await _client.PostAsJsonAsync("/api/orders", OrderBody(1, coupon: "BOJAN10"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        // Two units clear it.
        var accepted = await _client.PostAsJsonAsync("/api/orders", OrderBody(2, coupon: "BOJAN10"));
        accepted.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.SingleAsync();
            Assert.Equal(120_000, order.Discount.Amount);
            Assert.Equal(525_000, order.Total.Amount);
            Assert.Equal(1, (await db.Coupons.SingleAsync()).RedemptionCount);
        });
    }

    [Fact]
    public async Task The_coupon_check_prices_the_basket_itself()
    {
        await _factory.WithDbAsync(async db =>
            await TestData.AddCouponAsync(db, "TEN", percentOff: 10));

        var response = await _client.PostAsJsonAsync("/api/cart/coupon", new
        {
            code = "ten",
            lines = new[] { new { productId = _productId.ToString(), quantity = 2 } },
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // 10% of 600,000 — computed from database prices, not from anything the
        // client claimed the basket was worth.
        Assert.Equal("TEN", body.GetProperty("code").GetString());
        Assert.Equal(60_000, body.GetProperty("discount").GetInt64());
    }

    [Fact]
    public async Task An_unknown_coupon_is_a_non_2xx_not_a_valid_false_body()
    {
        var response = await _client.PostAsJsonAsync("/api/cart/coupon", new
        {
            code = "NOPE",
            lines = new[] { new { productId = _productId.ToString(), quantity = 1 } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Placing_an_order_without_a_credential_is_refused()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/orders", OrderBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Order_tracking_needs_the_number_and_the_phone_together()
    {
        await _client.PostAsJsonAsync("/api/orders", OrderBody(1));

        string number = null!;
        await _factory.WithDbAsync(async db => number = (await db.Orders.SingleAsync()).Number);

        using var anonymous = _factory.CreateClient();

        // The number alone must return nothing — that is what stops the
        // endpoint being an order-number enumeration vector.
        var numberOnly = await anonymous.GetAsync($"/api/orders/track?number={number}");
        Assert.Equal(HttpStatusCode.BadRequest, numberOnly.StatusCode);

        var wrongPhone = await anonymous.GetAsync($"/api/orders/track?number={number}&phone=09120000000");
        Assert.Equal(HttpStatusCode.NotFound, wrongPhone.StatusCode);

        var matched = await anonymous.GetAsync($"/api/orders/track?number={number}&phone=09121110010");
        matched.EnsureSuccessStatusCode();

        var body = await matched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(number, body.GetProperty("number").GetString());
    }
}
