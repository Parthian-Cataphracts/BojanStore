using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The loyalty club, end to end.
/// </summary>
/// <remarks>
/// <para>
/// It used to be a page and nothing else. <c>/loyalty</c> advertised three
/// tiers, "۵٪ تخفیف دائمی" and "ارسال رایگان نامحدود"; nothing applied any of
/// it, and <c>AddLoyaltyPoints</c> had exactly one caller in the whole codebase
/// — the seeder. No order earned a point, no member moved a tier, no discount
/// was ever taken off anything.
/// </para>
/// <para>
/// These cover the loop that makes the page true: an order is delivered, the
/// member earns, the tier they reach discounts the next order, and the points
/// are paid exactly once however many times the order is moved.
/// </para>
/// </remarks>
public sealed class LoyaltyProgrammeTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private HttpClient _owner = null!;
    private Guid _customerId;
    private Guid _addressId;
    private Guid _productId;
    private Guid _ownerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 300_000, stock: 50);
            var customer = await TestData.AddCustomerAsync(db, "09121110010");
            var address = await TestData.AddAddressAsync(db, customer.Id);
            await TestData.AddCheckoutMethodsAsync(db);

            var owner = await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@example.com");

            _productId = product.Id;
            _customerId = customer.Id;
            _addressId = address.Id;
            _ownerId = owner.Id;
        });

        _client = _factory.CreateCustomerClient(_customerId);
        _owner = _factory.CreateAdminClient(_ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _owner?.Dispose();
        _factory.Dispose();
    }

    private object OrderBody(int quantity = 2) => new
    {
        lines = new[] { new { productId = _productId.ToString(), quantity } },
        addressId = _addressId.ToString(),
        shippingMethodId = "standard",
        paymentMethodId = "cod",
    };

    private Task<HttpResponseMessage> SaveClubAsync(int tomanPerPoint, params object[] tiers) =>
        _owner.PostAsJsonAsync("/api/admin/loyalty", new { tomanPerPoint, tiers });

    private Task GivePointsAsync(int points) =>
        _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);
            customer.AddLoyaltyPoints(points);
            await db.SaveChangesAsync();
        });

    // --- what the club is worth ---------------------------------------------

    [Fact]
    public async Task A_members_tier_discounts_their_order()
    {
        (await SaveClubAsync(10_000,
            new { name = "برنزی", minimumPoints = 0, discountPercent = 0, freeShipping = false },
            new { name = "نقره‌ای", minimumPoints = 1_000, discountPercent = 5, freeShipping = false }))
            .EnsureSuccessStatusCode();

        await GivePointsAsync(1_200);

        (await _client.PostAsJsonAsync("/api/orders", OrderBody())).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.SingleAsync();

            // 5% of 600,000.
            Assert.Equal(30_000, order.LoyaltyDiscount.Amount);
            Assert.Equal(600_000 - 30_000 + 45_000, order.Total.Amount);
        });
    }

    /// <summary>
    /// A member below every rung pays the ordinary price. This is the state most
    /// shoppers are in, so it is the one that decides what the shop takes.
    /// </summary>
    [Fact]
    public async Task A_shopper_with_no_points_is_charged_the_ordinary_price()
    {
        (await SaveClubAsync(10_000,
            new { name = "نقره‌ای", minimumPoints = 1_000, discountPercent = 5, freeShipping = false }))
            .EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync("/api/orders", OrderBody())).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.SingleAsync();

            Assert.Equal(0, order.LoyaltyDiscount.Amount);
            Assert.Equal(600_000 + 45_000, order.Total.Amount);
        });
    }

    /// <summary>
    /// The tier the page advertised as "ارسال رایگان نامحدود", finally meaning
    /// something.
    /// </summary>
    [Fact]
    public async Task A_tier_granting_free_delivery_waives_it_whatever_the_method_costs()
    {
        (await SaveClubAsync(10_000,
            new { name = "طلایی", minimumPoints = 3_000, discountPercent = 10, freeShipping = true }))
            .EnsureSuccessStatusCode();

        await GivePointsAsync(3_000);

        (await _client.PostAsJsonAsync("/api/orders", OrderBody())).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.SingleAsync();

            Assert.Equal(0, order.Shipping.Amount);
            Assert.Equal(60_000, order.LoyaltyDiscount.Amount);
            Assert.Equal(540_000, order.Total.Amount);
        });
    }

    /// <summary>
    /// A coupon and a tier are different promises — a campaign the shopper opted
    /// into, and a benefit of belonging — so the order records them apart. A
    /// refund that could not tell them apart would give back the wrong thing.
    /// </summary>
    [Fact]
    public async Task A_coupon_and_a_tier_are_recorded_separately()
    {
        (await SaveClubAsync(10_000,
            new { name = "نقره‌ای", minimumPoints = 1_000, discountPercent = 5, freeShipping = false }))
            .EnsureSuccessStatusCode();

        await GivePointsAsync(1_000);

        await _factory.WithDbAsync(async db =>
        {
            await TestData.AddCouponAsync(db, "WELCOME", percentOff: 10);
        });

        var body = new
        {
            lines = new[] { new { productId = _productId.ToString(), quantity = 2 } },
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "cod",
            couponCode = "WELCOME",
        };

        (await _client.PostAsJsonAsync("/api/orders", body)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.SingleAsync();

            // The coupon comes off first (10% of 600,000), then the tier's 5%
            // off what is left.
            Assert.Equal(60_000, order.Discount.Amount);
            Assert.Equal(27_000, order.LoyaltyDiscount.Amount);
        });
    }

    // --- what a member earns -------------------------------------------------

    /// <summary>
    /// Earned on delivery, not on placement: an order that is cancelled or never
    /// paid for has bought the member nothing, and awarding at checkout would
    /// let someone climb the tiers by ordering and refusing every parcel.
    /// </summary>
    [Fact]
    public async Task Points_are_earned_when_the_order_is_delivered_and_not_before()
    {
        (await SaveClubAsync(10_000,
            new { name = "برنزی", minimumPoints = 0, discountPercent = 0, freeShipping = false }))
            .EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync("/api/orders", OrderBody())).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).LoyaltyPoints));

        await DeliverTheOrderAsync();

        await _factory.WithDbAsync(async db =>
            // 600,000 at one point per 10,000.
            Assert.Equal(60, (await db.Customers.SingleAsync(c => c.Id == _customerId)).LoyaltyPoints));
    }

    /// <summary>
    /// The guard that matters. An operator moving a delivered order about, or a
    /// retried job, must not pay the member twice — and the guard is a column on
    /// the order rather than a hope about how often this runs.
    /// </summary>
    [Fact]
    public async Task Delivering_twice_pays_the_member_once()
    {
        (await SaveClubAsync(10_000,
            new { name = "برنزی", minimumPoints = 0, discountPercent = 0, freeShipping = false }))
            .EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync("/api/orders", OrderBody())).EnsureSuccessStatusCode();

        await DeliverTheOrderAsync();
        await _owner.PostAsJsonAsync("/api/admin/orders/status", new { id = await OrderIdAsync(), status = "delivered" });

        await _factory.WithDbAsync(async db =>
            Assert.Equal(60, (await db.Customers.SingleAsync(c => c.Id == _customerId)).LoyaltyPoints));
    }

    /// <summary>
    /// How an owner pauses the club without deleting anyone's balance: members
    /// keep what they have and stop accruing.
    /// </summary>
    [Fact]
    public async Task A_rate_of_zero_earns_nothing()
    {
        (await SaveClubAsync(0,
            new { name = "برنزی", minimumPoints = 0, discountPercent = 0, freeShipping = false }))
            .EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync("/api/orders", OrderBody())).EnsureSuccessStatusCode();
        await DeliverTheOrderAsync();

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).LoyaltyPoints));
    }

    // --- configuring it ------------------------------------------------------

    [Fact]
    public async Task The_club_is_readable_without_a_credential()
    {
        (await SaveClubAsync(10_000,
            new { name = "برنزی", minimumPoints = 0, discountPercent = 0, freeShipping = false }))
            .EnsureSuccessStatusCode();

        using var anonymous = _factory.CreateClient();
        var club = await anonymous.GetFromJsonAsync<JsonElement>("/api/loyalty");

        Assert.True(club.GetProperty("enabled").GetBoolean());
        Assert.Equal(10_000, club.GetProperty("tomanPerPoint").GetInt32());
        Assert.Single(club.GetProperty("tiers").EnumerateArray());
    }

    /// <summary>
    /// A shop that has configured no tiers has no club, and the page says
    /// nothing rather than drawing an empty ladder under a heading about rewards.
    /// </summary>
    [Fact]
    public async Task A_shop_with_no_tiers_reports_the_club_as_off()
    {
        using var anonymous = _factory.CreateClient();
        var club = await anonymous.GetFromJsonAsync<JsonElement>("/api/loyalty");

        Assert.False(club.GetProperty("enabled").GetBoolean());
        Assert.Empty(club.GetProperty("tiers").EnumerateArray());
    }

    /// <summary>
    /// A rung that gives less than the one below it means spending more to be
    /// worse off — a row typed on the wrong line, not an offer.
    /// </summary>
    [Fact]
    public async Task A_tier_that_is_worse_than_the_one_below_it_is_refused()
    {
        var response = await SaveClubAsync(10_000,
            new { name = "نقره‌ای", minimumPoints = 1_000, discountPercent = 10, freeShipping = false },
            new { name = "طلایی", minimumPoints = 3_000, discountPercent = 5, freeShipping = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Two_tiers_at_one_point_total_are_refused()
    {
        var response = await SaveClubAsync(10_000,
            new { name = "الف", minimumPoints = 1_000, discountPercent = 5, freeShipping = false },
            new { name = "ب", minimumPoints = 1_000, discountPercent = 8, freeShipping = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>The ceiling that stops the club selling the shop's stock at a loss.</summary>
    [Fact]
    public async Task A_discount_beyond_the_ceiling_is_refused()
    {
        var response = await SaveClubAsync(10_000,
            new { name = "بی‌حساب", minimumPoints = 0, discountPercent = 80, freeShipping = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Owner only, beside shipping and payment: a tier sets a standing discount
    /// on every order every member ever places.
    /// </summary>
    [Fact]
    public async Task An_operator_who_is_not_the_owner_cannot_change_the_club()
    {
        Guid salesId = default;
        await _factory.WithDbAsync(async db =>
            salesId = (await TestData.AddAdminAsync(db, AdminRole.Sales, "sales@example.com")).Id);

        using var sales = _factory.CreateAdminClient(salesId);

        var response = await sales.PostAsJsonAsync("/api/admin/loyalty", new
        {
            tomanPerPoint = 1,
            tiers = new[] { new { name = "خودم", minimumPoints = 0, discountPercent = 50, freeShipping = true } },
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- helpers -------------------------------------------------------------

    private async Task<string> OrderIdAsync()
    {
        var id = string.Empty;
        await _factory.WithDbAsync(async db => id = (await db.Orders.SingleAsync()).Id.ToString());
        return id;
    }

    /// <summary>Walks the order forward the way an operator does — the domain only moves it one step at a time.</summary>
    private async Task DeliverTheOrderAsync()
    {
        var id = await OrderIdAsync();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.SingleAsync();
            order.MarkPaid(DateTimeOffset.UtcNow, "test", null);
            await db.SaveChangesAsync();
        });

        foreach (var status in new[] { "processing", "shipped", "delivered" })
        {
            var response = await _owner.PostAsJsonAsync("/api/admin/orders/status", new { id, status });
            response.EnsureSuccessStatusCode();
        }
    }
}
