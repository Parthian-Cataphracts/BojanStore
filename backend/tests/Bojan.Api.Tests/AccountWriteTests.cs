using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Orders;
using Bojan.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Phase 5 — the customer and public writes.
/// </summary>
public sealed class AccountWriteTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _customerId;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 300_000, stock: 5);
            var customer = await TestData.AddCustomerAsync(db, "09121110030");

            _productId = product.Id;
            _customerId = customer.Id;
        });

        _client = _factory.CreateCustomerClient(_customerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Updating_the_profile_leaves_the_fields_it_was_not_sent_alone()
    {
        var response = await _client.PutAsJsonAsync("/api/me", new { city = "اصفهان" });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("اصفهان", body.GetProperty("city").GetString());
        // The form posts only what it shows; a partial body must not blank the
        // rest of the profile.
        Assert.Equal("آزمون", body.GetProperty("firstName").GetString());
    }

    /// <summary>
    /// The avatar field takes a URL this API issued, and nothing else.
    /// </summary>
    /// <remarks>
    /// It is the one profile field whose value is a URL, so it is the one that
    /// could otherwise point anywhere — an off-site tracker that fires whenever
    /// an operator opens the customer, or a <c>data:</c> payload that was never
    /// sniffed or stored here. Shape alone is not enough: the check is that the
    /// upload endpoint produced it.
    /// </remarks>
    [Theory]
    [InlineData("https://evil.example/pixel.png")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("/media/products/0123456789abcdef0123456789abcdef.jpg")]
    [InlineData("/media/avatars/../products/0123456789abcdef0123456789abcdef.jpg")]
    [InlineData("/media/avatars/not-a-generated-name.jpg")]
    public async Task An_avatar_the_uploader_did_not_produce_is_refused(string avatar)
    {
        var response = await _client.PutAsJsonAsync("/api/me", new { avatar });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Null((await db.Customers.SingleAsync(c => c.Id == _customerId)).AvatarUrl));
    }

    [Fact]
    public async Task An_avatar_from_the_upload_endpoint_is_stored_and_can_be_cleared()
    {
        var uploaded = "/media/avatars/0123456789abcdef0123456789abcdef.jpg";

        var saved = await _client.PutAsJsonAsync("/api/me", new { avatar = uploaded });
        saved.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Equal(uploaded, (await db.Customers.SingleAsync(c => c.Id == _customerId)).AvatarUrl));

        var cleared = await _client.PutAsJsonAsync("/api/me", new { avatar = "" });
        cleared.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Null((await db.Customers.SingleAsync(c => c.Id == _customerId)).AvatarUrl));
    }

    [Fact]
    public async Task A_malformed_national_id_is_a_field_error()
    {
        var response = await _client.PutAsJsonAsync("/api/me", new { nationalId = "12" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_first_address_a_customer_saves_becomes_their_default()
    {
        var response = await _client.PostAsJsonAsync("/api/me/addresses", new
        {
            title = "خانه",
            recipient = "آزمون کاربر",
            phone = "09121234567",
            province = "تهران",
            city = "تهران",
            postalCode = "1234567890",
            line = "خیابان آزمایشی، پلاک ۱",
            isDefault = false,
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // A customer with addresses and no default breaks the checkout's
        // pre-selection, so the first one is default whatever the box said.
        Assert.True(body.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task Deleting_an_address_that_belongs_to_someone_else_is_not_found()
    {
        Guid strangersAddress = default;

        await _factory.WithDbAsync(async db =>
        {
            var other = await TestData.AddCustomerAsync(db, "09121110031");
            strangersAddress = (await TestData.AddAddressAsync(db, other.Id)).Id;
        });

        var response = await _client.PostAsJsonAsync("/api/me/addresses/delete", new { id = strangersAddress.ToString() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Equal(1, await db.Addresses.CountAsync()));
    }

    [Fact]
    public async Task A_review_lands_pending_and_stays_off_the_product_page()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews", new
        {
            productSlug = "p-01",
            rating = 5,
            body = "عالی بود",
            recommend = true,
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var review = await db.ProductReviews.SingleAsync();
            // BACKEND.md Phase 5: reviews need a moderation state.
            Assert.Equal(ModerationStatus.Pending, review.Status);
            // Nothing was bought, so no verified-purchase badge.
            Assert.False(review.IsVerifiedPurchase);
        });

        using var anonymous = _factory.CreateClient();
        var published = await (await anonymous.GetAsync("/api/products/p-01/reviews"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, published.GetArrayLength());
    }

    [Fact]
    public async Task The_same_customer_cannot_review_a_product_twice()
    {
        var body = new { productSlug = "p-01", rating = 4, body = "خوب", recommend = true };

        await _client.PostAsJsonAsync("/api/reviews", body);
        var second = await _client.PostAsJsonAsync("/api/reviews", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_review_of_a_delivered_order_is_marked_a_verified_purchase()
    {
        await _factory.WithDbAsync(async db =>
        {
            var address = await TestData.AddAddressAsync(db, _customerId);
            var product = await db.Products.SingleAsync();

            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                _customerId,
                [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 1, product.Price)],
                address.Id,
                "تهران",
                "ارسال استاندارد",
                "پرداخت در محل",
                product.Price,
                Domain.Common.Money.Zero,
                Domain.Common.Money.Zero,
                Guid.NewGuid().ToString());

            order.TransitionTo(OrderStatus.Delivered);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        });

        await _client.PostAsJsonAsync("/api/reviews", new
        {
            productSlug = "p-01",
            rating = 5,
            body = "رسید و عالی بود",
            recommend = true,
        });

        await _factory.WithDbAsync(async db =>
            Assert.True((await db.ProductReviews.SingleAsync()).IsVerifiedPurchase));
    }

    [Fact]
    public async Task A_delivered_product_shows_up_as_awaiting_a_review()
    {
        await _factory.WithDbAsync(async db =>
        {
            var address = await TestData.AddAddressAsync(db, _customerId);
            var product = await db.Products.SingleAsync();

            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                _customerId,
                [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 1, product.Price)],
                address.Id,
                "تهران",
                "ارسال استاندارد",
                "پرداخت در محل",
                product.Price,
                Domain.Common.Money.Zero,
                Domain.Common.Money.Zero,
                Guid.NewGuid().ToString());

            order.TransitionTo(OrderStatus.Delivered);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        });

        var awaiting = await (await _client.GetAsync("/api/me/reviews/awaiting"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, awaiting.GetArrayLength());
        Assert.Equal("p-01", awaiting[0].GetProperty("productSlug").GetString());

        await _client.PostAsJsonAsync("/api/reviews", new
        {
            productSlug = "p-01",
            rating = 5,
            body = "نوشتم",
            recommend = true,
        });

        // Once reviewed it drops off the list — the anti-join runs in SQL.
        var after = await (await _client.GetAsync("/api/me/reviews/awaiting"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, after.GetArrayLength());
    }

    [Fact]
    public async Task A_stock_alert_needs_a_way_to_reach_the_customer()
    {
        using var anonymous = _factory.CreateClient();

        var missingContact = await anonymous.PostAsJsonAsync("/api/stock-alerts", new { productSlug = "p-01" });
        Assert.Equal(HttpStatusCode.BadRequest, missingContact.StatusCode);

        var accepted = await anonymous.PostAsJsonAsync(
            "/api/stock-alerts",
            new { productSlug = "p-01", phone = "09121110099" });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        // Asking twice is the same request, not a second SMS to queue.
        await anonymous.PostAsJsonAsync("/api/stock-alerts", new { productSlug = "p-01", phone = "09121110099" });

        await _factory.WithDbAsync(async db => Assert.Equal(1, await db.StockAlerts.CountAsync()));
    }

    [Fact]
    public async Task The_contact_form_opens_a_thread_with_the_message_in_it()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/support/messages", new
        {
            name = "بازدیدکننده",
            phone = "09121110098",
            subject = "سوال درباره ارسال",
            body = "چند روز طول می‌کشد؟",
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var ticket = await db.SupportTickets.Include(t => t.Messages).SingleAsync();
            // The body is the thread's first message, not a field on the
            // ticket, so a reply continues one conversation.
            Assert.Single(ticket.Messages);
            Assert.False(ticket.Messages.Single().FromSupport);
            Assert.Null(ticket.CustomerId);
        });
    }

    [Fact]
    public async Task A_business_request_from_a_visitor_belongs_to_nobody()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/business/requests", new
        {
            organization = "شرکت آزمایشی",
            contact = "مدیر خرید",
            phone = "09121110097",
            items = "حدود ۵۰ عدد",
            description = "تجهیز دفتر جدید",
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.StartsWith("B2B-", body.GetProperty("code").GetString());
        Assert.Equal("submitted", body.GetProperty("status").GetString());
        Assert.Equal(50, body.GetProperty("itemCount").GetInt32());

        // An anonymous submission appears in the panel, never in an account.
        var mine = await (await _client.GetAsync("/api/business/requests")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, mine.GetArrayLength());
    }

    [Fact]
    public async Task Marking_notifications_read_only_touches_the_callers_own()
    {
        await _factory.WithDbAsync(async db =>
        {
            var other = await TestData.AddCustomerAsync(db, "09121110032");

            db.CustomerNotifications.Add(new Domain.Customers.CustomerNotification
            {
                CustomerId = _customerId,
                Kind = Domain.Customers.NotificationKind.Offer,
                Title = "برای من",
                Body = "متن",
            });

            db.CustomerNotifications.Add(new Domain.Customers.CustomerNotification
            {
                CustomerId = other.Id,
                Kind = Domain.Customers.NotificationKind.Offer,
                Title = "برای دیگری",
                Body = "متن",
            });

            await db.SaveChangesAsync();
        });

        // No ids means "all of mine" — screen 53's header action.
        var response = await _client.PostAsJsonAsync("/api/me/notifications/read", new { ids = Array.Empty<string>() });
        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            Assert.True(await db.CustomerNotifications.SingleAsync(n => n.Title == "برای من") is { IsRead: true });
            Assert.False((await db.CustomerNotifications.SingleAsync(n => n.Title == "برای دیگری")).IsRead);
        });
    }

    /// <summary>
    /// The sandbox gateway approves any payment without a bank in the loop —
    /// crediting the wallet from that would let any signed-in customer mint
    /// spendable balance for free, so the top-up refuses outright while it is
    /// the gateway in use, the same way <see cref="PaymentGatewayGateTests"/>
    /// covers the equivalent gate at startup.
    /// </summary>
    [Fact]
    public async Task Topping_up_the_wallet_is_refused_while_the_sandbox_gateway_is_in_use()
    {
        var response = await _client.PostAsJsonAsync("/api/me/wallet/topup", new { amount = 250_000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);
            Assert.Equal(0, customer.WalletBalance.Amount);
            Assert.Equal(0, await db.WalletTransactions.CountAsync(t => t.CustomerId == _customerId));
        });
    }

    /// <summary>A non-positive amount is refused rather than silently crediting nothing.</summary>
    [Fact]
    public async Task Topping_up_the_wallet_with_a_zero_amount_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/me/wallet/topup", new { amount = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount));
    }
}
