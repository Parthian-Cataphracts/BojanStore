using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Api.Tests;

/// <summary>
/// The three invoice endpoints, over real HTTP.
/// </summary>
/// <remarks>
/// What these cover that <c>InvoiceBuilderTests</c> cannot: that the number is
/// persisted and unique, that the list finds an invoice by a Persian-typed
/// number, and that the two gates in front of the document hold — a customer
/// cannot read someone else's, and an operator without the orders section
/// cannot read any.
/// </remarks>
public sealed class InvoiceEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _customer = null!;
    private HttpClient _stranger = null!;

    private Guid _deliveredId;
    private Guid _pendingId;
    private Guid _productId;
    private string _invoiceNumber = null!;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        Guid strangerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 200_000, stock: 10);
            _productId = product.Id;

            var customer = await TestData.AddCustomerAsync(db, "09121110050");
            var address = await TestData.AddAddressAsync(db, customer.Id);
            _customerId = customer.Id;

            var delivered = MakeOrder(customer.Id, address.Id, product, OrderStatus.Delivered);
            var pending = MakeOrder(customer.Id, address.Id, product, OrderStatus.Pending);
            db.Orders.AddRange(delivered, pending);

            _deliveredId = delivered.Id;
            _pendingId = pending.Id;
            _invoiceNumber = delivered.InvoiceNumber!;

            // Someone else's order, to prove the storefront gate is on the
            // customer and not merely on the order existing.
            var other = await TestData.AddCustomerAsync(db, "09121110051");
            strangerId = other.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
            await db.SaveChangesAsync();
        });

        _admin = _factory.CreateAdminClient(ownerId);
        _customer = _factory.CreateCustomerClient(_customerId);
        _stranger = _factory.CreateCustomerClient(strangerId);
    }

    private static Order MakeOrder(Guid customerId, Guid addressId, Domain.Catalogue.Product product, OrderStatus status)
    {
        var order = Order.Create(
            OrderNumber.NewOrderNumber(),
            customerId,
            [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 2, product.Price)],
            addressId,
            "تهران، خیابان آزمایشی",
            "ارسال استاندارد",
            "پرداخت در محل",
            product.Price * 2,
            new Money(40_000),
            new Money(45_000),
            Guid.NewGuid().ToString());

        if (status != OrderStatus.Pending)
        {
            order.TransitionTo(status);
        }

        return order;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _admin?.Dispose();
        _customer?.Dispose();
        _stranger?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task The_invoice_list_holds_only_delivered_orders()
    {
        var body = await (await _admin.GetAsync("/api/admin/invoices")).Content.ReadFromJsonAsync<JsonElement>();

        // Two orders exist; only the delivered one has been invoiced.
        Assert.Equal(1, body.GetProperty("total").GetInt32());

        var row = body.GetProperty("items")[0];
        Assert.Equal(_invoiceNumber, row.GetProperty("invoiceNumber").GetString());
        Assert.Equal(16, row.GetProperty("invoiceNumber").GetString()!.Length);
        Assert.Equal("آزمون کاربر", row.GetProperty("customer").GetString());
        Assert.Equal(2, row.GetProperty("itemCount").GetInt32());
        // 400,000 of goods, less a 40,000 discount, plus 45,000 shipping.
        Assert.Equal(405_000, row.GetProperty("total").GetInt64());
    }

    [Fact]
    public async Task The_number_survives_the_round_trip_to_the_database()
    {
        // Read back through a fresh request rather than from the entity that
        // minted it: the column, its length and its filtered unique index are
        // the thing under test.
        var body = await (await _admin.GetAsync($"/api/admin/orders/{_deliveredId}/invoice"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(_invoiceNumber, body.GetProperty("invoiceNumber").GetString());
    }

    [Fact]
    public async Task The_list_finds_an_invoice_typed_in_Persian_digits()
    {
        var persian = new string(_invoiceNumber.Select(digit => (char)('۰' + (digit - '0'))).ToArray());

        var body = await (await _admin.GetAsync($"/api/admin/invoices?q={Uri.EscapeDataString(persian)}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("total").GetInt32());
        Assert.Equal(_invoiceNumber, body.GetProperty("items")[0].GetProperty("invoiceNumber").GetString());
    }

    [Fact]
    public async Task The_list_finds_an_invoice_by_order_number_and_by_customer_name()
    {
        var byName = await (await _admin.GetAsync("/api/admin/invoices?q=آزمون"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, byName.GetProperty("total").GetInt32());

        var missing = await (await _admin.GetAsync("/api/admin/invoices?q=9999999999999999"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, missing.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task An_order_that_has_not_been_delivered_has_no_invoice()
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _admin.GetAsync($"/api/admin/orders/{_pendingId}/invoice")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _customer.GetAsync($"/api/me/orders/{_pendingId}/invoice")).StatusCode);
    }

    [Fact]
    public async Task A_customer_reads_their_own_invoice_and_it_matches_the_panel_copy()
    {
        var mine = await (await _customer.GetAsync($"/api/me/orders/{_deliveredId}/invoice"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var theirs = await (await _admin.GetAsync($"/api/admin/orders/{_deliveredId}/invoice"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // The same document, not merely the same number — the two copies
        // disagreeing is the whole reason one contract serves both.
        Assert.Equal(theirs.GetRawText(), mine.GetRawText());

        Assert.Equal(405_000, mine.GetProperty("total").GetInt64());
        Assert.Equal("09121110050", mine.GetProperty("customerPhone").GetString());
        Assert.Equal(1, mine.GetProperty("lines").GetArrayLength());
        Assert.Equal(0, mine.GetProperty("returnedCount").GetInt32());
    }

    [Fact]
    public async Task A_customer_cannot_read_someone_elses_invoice()
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _stranger.GetAsync($"/api/me/orders/{_deliveredId}/invoice")).StatusCode);
    }

    [Fact]
    public async Task An_operator_who_cannot_open_orders_cannot_open_invoices_either()
    {
        Guid productOperatorId = default;
        await _factory.WithDbAsync(async db =>
        {
            productOperatorId = (await TestData.AddAdminAsync(db, AdminRole.Product, "product@bojan.test")).Id;
        });

        using var productOperator = _factory.CreateAdminClient(productOperatorId);

        // The catalogue role's policy does not carry orders, and an invoice is
        // a view of an order — so putting invoices behind their own section
        // would have opened a hole here rather than closed one.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await productOperator.GetAsync("/api/admin/invoices")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await productOperator.GetAsync($"/api/admin/orders/{_deliveredId}/invoice")).StatusCode);
    }

    [Fact]
    public async Task A_refunded_return_comes_off_the_invoice()
    {
        await _factory.WithDbAsync(async db =>
        {
            var request = ReturnRequest.Create(
                OrderNumber.NewReturnCode(),
                _customerId,
                _deliveredId,
                "BZ-TEST",
                "معیوب بود",
                null,
                "wallet",
                [new ReturnItem
                {
                    ReturnRequestId = Guid.Empty,
                    ProductId = _productId,
                    ProductSlug = "p-01",
                    ProductTitle = "کالا",
                    ProductImageUrl = "https://example.com/p.jpg",
                    Quantity = 1,
                }],
                DateTimeOffset.UtcNow);

            request.TransitionTo(ReturnStatus.Refunded, DateTimeOffset.UtcNow);
            db.ReturnRequests.Add(request);
            await db.SaveChangesAsync();
        });

        var body = await (await _customer.GetAsync($"/api/me/orders/{_deliveredId}/invoice"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // One of the two units back: 200,000 of goods, and a tenth off the
        // discount and shipping alike.
        Assert.Equal(1, body.GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        Assert.Equal(200_000, body.GetProperty("subtotal").GetInt64());
        Assert.Equal(20_000, body.GetProperty("discount").GetInt64());
        Assert.Equal(1, body.GetProperty("returnedCount").GetInt32());
        Assert.Equal(200_000, body.GetProperty("returnedRefund").GetInt64());
    }
}
