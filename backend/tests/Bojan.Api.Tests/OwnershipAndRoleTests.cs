using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The negative cases <c>BACKEND.md</c>'s definition of done asks for:
/// "Ownership and role checks are tested, including the negative case."
/// </summary>
public sealed class OwnershipAndRoleTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();

    private Guid _ownerCustomer;
    private Guid _strangerCustomer;
    private Guid _orderId;
    private string _orderNumber = string.Empty;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 200_000, stock: 10);

            var owner = await TestData.AddCustomerAsync(db, "09121110020");
            var stranger = await TestData.AddCustomerAsync(db, "09121110021");
            var address = await TestData.AddAddressAsync(db, owner.Id);

            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                owner.Id,
                [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 1, product.Price)],
                address.Id,
                "تهران، خیابان آزمایشی",
                "ارسال استاندارد",
                "پرداخت در محل",
                product.Price,
                Domain.Common.Money.Zero,
                new Domain.Common.Money(45_000),
                Guid.NewGuid().ToString());

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            _ownerCustomer = owner.Id;
            _strangerCustomer = stranger.Id;
            _orderId = order.Id;
            _orderNumber = order.Number;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_customer_reads_their_own_order()
    {
        using var client = _factory.CreateCustomerClient(_ownerCustomer);

        var response = await client.GetAsync($"/api/me/orders/{_orderId}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_orderNumber, body.GetProperty("number").GetString());
        // The five drawn fulfilment stages, whatever the order's own progress.
        Assert.Equal(5, body.GetProperty("timeline").GetArrayLength());
    }

    [Fact]
    public async Task The_order_number_works_as_an_identifier_too()
    {
        using var client = _factory.CreateCustomerClient(_ownerCustomer);

        var response = await client.GetAsync($"/api/me/orders/{_orderNumber}");

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 404, not 403 — <c>BACKEND.md</c> Phase 3: "a 403 confirms the order
    /// exists."
    /// </summary>
    [Fact]
    public async Task Someone_elses_order_is_not_found_rather_than_forbidden()
    {
        using var client = _factory.CreateCustomerClient(_strangerCustomer);

        var response = await client.GetAsync($"/api/me/orders/{_orderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Someone_elses_order_is_absent_from_their_list()
    {
        using var client = _factory.CreateCustomerClient(_strangerCustomer);

        var response = await client.GetAsync("/api/me/orders");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task An_account_read_without_a_credential_is_unauthorized()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/orders")).StatusCode);
    }

    /// <summary>
    /// A customer credential must not reach the panel — the <c>scope</c> claim
    /// is what separates them.
    /// </summary>
    [Fact]
    public async Task A_customer_credential_cannot_reach_an_admin_endpoint()
    {
        using var client = _factory.CreateCustomerClient(_ownerCustomer);

        var response = await client.GetAsync("/api/admin/orders");

        // Forbidden rather than unauthorized: the credential is valid, it is
        // simply a shopper's. Nothing about the panel is revealed either way.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_operator_with_the_right_role_may_write()
    {
        Guid productId = default;
        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            productId = (await db.Products.FirstAsync()).Id;
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
        });

        using var client = _factory.CreateAdminClient(ownerId);

        var response = await client.PostAsJsonAsync("/api/admin/products/pricing", new
        {
            id = productId.ToString(),
            price = 250_000,
            costPrice = 120_000,
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var product = await db.Products.FirstAsync(p => p.Id == productId);
            Assert.Equal(250_000, product.Price.Amount);
            Assert.Equal(120_000, product.CostPrice.Amount);

            // Every panel write leaves a trail — BACKEND.md Phase 7.
            var audit = await db.AuditEntries.SingleAsync();
            Assert.Equal("product.pricing.updated", audit.Action);
        });
    }

    /// <summary>
    /// The role gate's negative case. <c>support</c> is not on the
    /// <c>product-pricing</c> resource's list, so it must be refused here even
    /// though the panel would never have offered the screen.
    /// </summary>
    [Fact]
    public async Task An_operator_without_the_role_is_forbidden()
    {
        Guid productId = default;
        Guid supportId = default;

        await _factory.WithDbAsync(async db =>
        {
            productId = (await db.Products.FirstAsync()).Id;
            supportId = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@bojan.test")).Id;
        });

        using var client = _factory.CreateAdminClient(supportId);

        var response = await client.PostAsJsonAsync("/api/admin/products/pricing", new
        {
            id = productId.ToString(),
            price = 1,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(200_000, (await db.Products.FirstAsync(p => p.Id == productId)).Price.Amount));
    }

    /// <summary>
    /// A deactivated operator authenticates as nobody, whatever the proxy
    /// asserts — the role and the account's own state are read from this
    /// database, not from a header.
    /// </summary>
    [Fact]
    public async Task A_suspended_operator_cannot_get_in()
    {
        Guid suspendedId = default;

        await _factory.WithDbAsync(async db =>
        {
            var admin = await TestData.AddAdminAsync(db, AdminRole.Owner, "suspended@bojan.test");
            admin.IsActive = false;
            await db.SaveChangesAsync();
            suspendedId = admin.Id;
        });

        using var client = _factory.CreateAdminClient(suspendedId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/orders")).StatusCode);
    }

    /// <summary>
    /// An identity header with no API key authenticates nobody — that is the
    /// whole difference between "the API trusts the Next server" and "the API
    /// trusts whatever id you send".
    /// </summary>
    [Fact]
    public async Task An_identity_header_without_the_api_key_is_ignored()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", _ownerCustomer.ToString());

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_cost_price_never_appears_in_a_storefront_response()
    {
        await _factory.WithDbAsync(async db =>
        {
            var product = await db.Products.FirstAsync();
            product.CostPrice = new Domain.Common.Money(90_000);
            await db.SaveChangesAsync();
        });

        using var client = _factory.CreateClient();
        var body = await (await client.GetAsync("/api/products/p-01")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.TryGetProperty("costPrice", out _));
    }
}
