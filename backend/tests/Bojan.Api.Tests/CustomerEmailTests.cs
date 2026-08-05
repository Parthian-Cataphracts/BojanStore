using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Api.Tests;

/// <summary>
/// That the events actually send, over real HTTP.
/// </summary>
/// <remarks>
/// <see cref="EmailTemplateTests"/> proves the messages render. These prove
/// they are reached — which is the half that breaks silently, because a hook
/// that was never wired looks exactly like one that was.
/// </remarks>
public sealed class CustomerEmailTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _customer = null!;

    private Guid _customerId;
    private Guid _orderId;
    private Guid _productId;

    private const string Address = "niloofar@example.com";

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 200_000, stock: 10);
            _productId = product.Id;

            var customer = await TestData.AddCustomerAsync(db, "09121110070");
            customer.Email = Address;
            _customerId = customer.Id;

            var address = await TestData.AddAddressAsync(db, customer.Id);

            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                customer.Id,
                [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 2, product.Price)],
                address.Id,
                "تهران، خیابان آزمایشی",
                "ارسال استاندارد",
                "پرداخت در محل",
                product.Price * 2,
                Money.Zero,
                new Money(45_000),
                Guid.NewGuid().ToString());

            db.Orders.Add(order);
            _orderId = order.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
            await db.SaveChangesAsync();
        });

        _admin = _factory.CreateAdminClient(ownerId);
        _customer = _factory.CreateCustomerClient(_customerId);
        _factory.Email.Clear();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _admin?.Dispose();
        _customer?.Dispose();
        _factory.Dispose();
    }

    private async Task MoveOrderTo(string status)
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/orders/status", new
        {
            id = _orderId.ToString(),
            status,
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Shipping_an_order_emails_the_customer_the_tracking_code()
    {
        await _admin.PostAsJsonAsync("/api/admin/orders/status", new
        {
            id = _orderId.ToString(),
            status = "shipped",
            trackingCode = "24598731",
        });

        var sent = _factory.Email.LastFor(Address);

        Assert.NotNull(sent);
        Assert.Contains("ارسال شد", sent!.Subject, StringComparison.Ordinal);
        Assert.Contains("۲۴۵۹۸۷۳۱", sent.Body, StringComparison.Ordinal);
        Assert.NotNull(sent.Html);
    }

    [Fact]
    public async Task Delivering_an_order_emails_the_invoice_number()
    {
        await MoveOrderTo("delivered");

        var invoiceNumber = string.Empty;
        await _factory.WithDbAsync(async db =>
        {
            invoiceNumber = db.Orders.Single(o => o.Id == _orderId).InvoiceNumber!;
            await Task.CompletedTask;
        });

        var sent = _factory.Email.LastFor(Address);

        Assert.NotNull(sent);
        Assert.Contains("تحویل شد", sent!.Subject, StringComparison.Ordinal);

        // The number is issued by that same transition, so this is the first
        // and only time the customer is handed it.
        Assert.Contains(invoiceNumber, sent.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("processing")]
    [InlineData("packed")]
    public async Task The_statuses_that_are_not_news_send_nothing(string status)
    {
        await MoveOrderTo(status);

        // "Preparing" and "packed" are the shop talking to itself. Mailing them
        // trains a customer to ignore the ones that matter.
        Assert.Empty(_factory.Email.All);
    }

    [Fact]
    public async Task Cancelling_an_order_emails_what_moved()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/orders/cancel", new
        {
            id = _orderId.ToString(),
            reason = "موجود نبود",
            chargePenalty = false,
        });

        response.EnsureSuccessStatusCode();

        var sent = _factory.Email.LastFor(Address);

        Assert.NotNull(sent);
        Assert.Contains("لغو شد", sent!.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replying_to_a_ticket_emails_its_owner()
    {
        Guid threadId = default;
        await _factory.WithDbAsync(async db =>
        {
            var ticket = new Domain.Support.SupportTicket
            {
                CustomerId = _customerId,
                ContactName = "نیلوفر احمدی",
                Subject = "مشکل در تحویل",
            };

            db.SupportTickets.Add(ticket);
            threadId = ticket.Id;
            await db.SaveChangesAsync();
        });

        var response = await _admin.PostAsJsonAsync("/api/admin/support/replies", new
        {
            threadId = threadId.ToString(),
            body = "پاسخ آزمایشی",
        });

        response.EnsureSuccessStatusCode();

        var sent = _factory.Email.LastFor(Address);

        Assert.NotNull(sent);
        Assert.Contains("پشتیبانی", sent!.Subject, StringComparison.Ordinal);

        // Deliberately not the reply itself: a ticket can carry order or
        // account detail, and copying it into an inbox puts that outside the
        // account it belongs to.
        Assert.DoesNotContain("پاسخ آزمایشی", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_customer_with_no_address_is_skipped_rather_than_failing_the_operation()
    {
        await _factory.WithDbAsync(async db =>
        {
            // The shop's main sign-up path is a phone number, so this is the
            // normal case rather than an edge one.
            db.Customers.Single(c => c.Id == _customerId).Email = null;
            await db.SaveChangesAsync();
        });

        // The operation still succeeds — that is the whole contract.
        await MoveOrderTo("shipped");

        Assert.Empty(_factory.Email.All);
    }

    [Fact]
    public async Task Restocking_a_product_tells_everyone_who_asked_and_only_once()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.Products.Single(p => p.Id == _productId).Stock = 0;

            db.StockAlerts.Add(new StockAlert { ProductId = _productId, Email = "waiting@example.com" });
            db.StockAlerts.Add(new StockAlert { ProductId = _productId, Email = "alsowaiting@example.com" });

            // Already told about a previous restock — must not hear again.
            db.StockAlerts.Add(new StockAlert
            {
                ProductId = _productId,
                Email = "alreadytold@example.com",
                NotifiedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            });

            await db.SaveChangesAsync();
        });

        var response = await _admin.PostAsJsonAsync("/api/admin/inventory/movements", new
        {
            productId = _productId.ToString(),
            kind = "in",
            quantity = 5,
            reason = "ورود از تأمین‌کننده",
        });

        response.EnsureSuccessStatusCode();

        Assert.NotNull(_factory.Email.LastFor("waiting@example.com"));
        Assert.NotNull(_factory.Email.LastFor("alsowaiting@example.com"));
        Assert.Null(_factory.Email.LastFor("alreadytold@example.com"));

        // Stamped, so the next delivery does not mail them all over again —
        // the column existed and nothing ever set it.
        await _factory.WithDbAsync(async db =>
        {
            Assert.Empty(db.StockAlerts.Where(a => a.ProductId == _productId && a.NotifiedAtUtc == null));
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task A_delivery_into_a_product_that_was_already_in_stock_tells_nobody()
    {
        await _factory.WithDbAsync(async db =>
        {
            // Stock is 10 from the fixture — the product never went out.
            db.StockAlerts.Add(new StockAlert { ProductId = _productId, Email = "waiting@example.com" });
            await db.SaveChangesAsync();
        });

        await _admin.PostAsJsonAsync("/api/admin/inventory/movements", new
        {
            productId = _productId.ToString(),
            kind = "in",
            quantity = 5,
            reason = "ورود از تأمین‌کننده",
        });

        // "Back in stock" is a transition, not a state.
        Assert.Null(_factory.Email.LastFor("waiting@example.com"));
    }

    [Fact]
    public async Task Filing_a_return_emails_the_receipt()
    {
        await MoveOrderTo("delivered");
        _factory.Email.Clear();

        var response = await _customer.PostAsJsonAsync("/api/me/returns", new
        {
            orderId = _orderId.ToString(),
            items = new[] { new { productId = _productId.ToString(), quantity = 1 } },
            reason = "کالا معیوب بود",
            description = (string?)null,
            refundMethod = "wallet",
        });

        response.EnsureSuccessStatusCode();

        var sent = _factory.Email.LastFor(Address);

        Assert.NotNull(sent);
        Assert.Contains("مرجوعی", sent!.Subject, StringComparison.Ordinal);
        Assert.Contains("RT-", sent.Body, StringComparison.Ordinal);
    }
}
