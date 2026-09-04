using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Inventory;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The tick-boxes on screen 96 — archiving and deleting several products at
/// once.
/// </summary>
/// <remarks>
/// <para>
/// The list offered one verb before this: open a product, change its status,
/// come back. Retiring a season's stock was that, forty times over.
/// </para>
/// <para>
/// The two verbs are deliberately not the same operation. Archiving is the soft
/// delete — gone from the shop, still on every invoice that sold it — and works
/// on anything. Deleting takes the row away and is refused for a product that
/// has ever been ordered, counted in a stocktake or returned, because those are
/// the three references the schema will not cascade and an invoice whose
/// product is gone is not a tidier invoice.
/// </para>
/// </remarks>
public sealed class ProductBulkActionsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _brandId;
    private Guid _categoryId;
    private Guid _ownerId;
    private Guid _penId;
    private Guid _pencilId;
    private Guid _rulerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            (_brandId, _categoryId) = await TestData.AddCatalogueAsync(db);

            _penId = (await TestData.AddProductAsync(db, _brandId, _categoryId, "pen", 50_000, stock: 5)).Id;
            _pencilId = (await TestData.AddProductAsync(db, _brandId, _categoryId, "pencil", 30_000, stock: 5)).Id;
            _rulerId = (await TestData.AddProductAsync(db, _brandId, _categoryId, "ruler", 20_000, stock: 5)).Id;

            _ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "bulk-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(_ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    // --- archiving -----------------------------------------------------------

    [Fact]
    public async Task Archiving_a_batch_takes_every_one_of_them_out_of_the_shop()
    {
        var response = await StatusAsync("archived", _penId, _pencilId);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkResult>();
        Assert.Equal(2, result!.Changed);

        Assert.True(await IsArchivedAsync(_penId));
        Assert.True(await IsArchivedAsync(_pencilId));

        // And the one nobody ticked is untouched, which is the whole point of a
        // selection.
        Assert.False(await IsArchivedAsync(_rulerId));
    }

    [Fact]
    public async Task An_archived_batch_can_be_brought_back_as_drafts()
    {
        (await StatusAsync("archived", _penId)).EnsureSuccessStatusCode();

        (await StatusAsync("draft", _penId)).EnsureSuccessStatusCode();

        var product = await _factory.WithDbAsync(db =>
            db.Products.IgnoreQueryFilters().AsNoTracking().FirstAsync(p => p.Id == _penId));

        Assert.Null(product.DeletedAtUtc);

        // Out of the archive is not back on the shopfront: coming back as a
        // draft is what leaves that decision with a person.
        Assert.False(product.IsPublished);
    }

    [Fact]
    public async Task A_status_nobody_recognises_changes_nothing()
    {
        var response = await StatusAsync("retired", _penId, _pencilId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await IsArchivedAsync(_penId));
        Assert.False(await IsArchivedAsync(_pencilId));
    }

    [Fact]
    public async Task A_page_whose_products_have_all_gone_is_a_404()
    {
        var response = await StatusAsync("archived", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archiving_leaves_a_row_in_the_audit_trail_for_each_product()
    {
        (await StatusAsync("archived", _penId, _pencilId)).EnsureSuccessStatusCode();

        var targets = await _factory.WithDbAsync(db => db.AuditEntries
            .AsNoTracking()
            .Where(entry => entry.Action == "product.archived")
            .Select(entry => entry.Target)
            .ToListAsync());

        // One row per product, not one naming both: the trail is read to answer
        // "who archived this?", and a single row for a batch answers it for
        // neither of them.
        Assert.Equal(["pen", "pencil"], targets.Order());
    }

    // --- deleting ------------------------------------------------------------

    [Fact]
    public async Task Deleting_a_batch_takes_the_rows_away()
    {
        var response = await DeleteAsync(_penId, _pencilId);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkResult>();
        Assert.Equal(2, result!.Changed);
        Assert.Empty(result.Blocked);

        Assert.Equal(0, await CountAsync(_penId, _pencilId));

        // Not a soft delete in disguise — the archived filter cannot find them
        // either.
        Assert.Equal(1, await _factory.WithDbAsync(db =>
            db.Products.IgnoreQueryFilters().CountAsync(p => p.Id == _rulerId)));
    }

    [Fact]
    public async Task A_product_that_has_sold_is_kept_and_named()
    {
        await PlaceOrderForAsync(_penId);

        var response = await DeleteAsync(_penId, _pencilId);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkResult>();

        // Partial rather than all-or-nothing: the operator asked for both to go
        // and one of them can, so refusing the pair would leave them deleting
        // one at a time to find which is stuck.
        Assert.Equal(1, result!.Changed);
        Assert.Equal(["محصول pen"], result.Blocked);

        Assert.Equal(1, await CountAsync(_penId));
        Assert.Equal(0, await CountAsync(_pencilId));
    }

    [Fact]
    public async Task A_product_counted_in_a_stocktake_is_kept_too()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = _penId,
                Kind = StockMovementKind.Adjust,
                Quantity = 4,
                Reason = "انبارگردانی",
                ActorId = _ownerId,
            });
            await db.SaveChangesAsync();
        });

        var response = await DeleteAsync(_penId, _pencilId);
        response.EnsureSuccessStatusCode();

        Assert.Equal(1, await CountAsync(_penId));
        Assert.Equal(0, await CountAsync(_pencilId));
    }

    [Fact]
    public async Task A_batch_where_everything_has_traded_is_refused_by_name()
    {
        await PlaceOrderForAsync(_penId);

        var response = await DeleteAsync(_penId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // The key the panel maps to "بایگانی‌شان کنید" — a bare `conflict`
        // would arrive as "این مقدار از قبل ثبت شده است", which says neither
        // what happened nor what to do instead.
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("conflict", problem!.Title);
        Assert.Equal("product-has-history", problem.Detail);

        Assert.Equal(1, await CountAsync(_penId));
    }

    [Fact]
    public async Task Deleting_a_product_is_still_available_to_archive_instead()
    {
        await PlaceOrderForAsync(_penId);

        // The sentence the panel shows tells the operator to archive; this is
        // that instruction actually working on the same product.
        (await StatusAsync("archived", _penId)).EnsureSuccessStatusCode();

        Assert.True(await IsArchivedAsync(_penId));
    }

    // --- who may do it -------------------------------------------------------

    [Theory]
    [InlineData("/api/admin/products/status")]
    [InlineData("/api/admin/products/delete")]
    public async Task Neither_verb_is_open_to_an_operator_outside_the_catalogue(string path)
    {
        var supportId = await _factory.WithDbAsync(async db =>
            (await TestData.AddAdminAsync(db, AdminRole.Support, "bulk-support@bojan.test")).Id);

        using var support = _factory.CreateAdminClient(supportId);

        var response = await support.PostAsJsonAsync(
            path,
            new { ids = new[] { _penId.ToString() }, status = "archived" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await IsArchivedAsync(_penId));
        Assert.Equal(1, await CountAsync(_penId));
    }

    // --- helpers -------------------------------------------------------------

    private Task<HttpResponseMessage> StatusAsync(string status, params Guid[] ids) =>
        _client.PostAsJsonAsync(
            "/api/admin/products/status",
            new { ids = ids.Select(id => id.ToString()).ToArray(), status });

    private Task<HttpResponseMessage> DeleteAsync(params Guid[] ids) =>
        _client.PostAsJsonAsync(
            "/api/admin/products/delete",
            new { ids = ids.Select(id => id.ToString()).ToArray() });

    private Task<bool> IsArchivedAsync(Guid id) => _factory.WithDbAsync(db => db.Products
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(p => p.Id == id)
        .Select(p => p.DeletedAtUtc != null)
        .FirstAsync());

    private Task<int> CountAsync(params Guid[] ids) => _factory.WithDbAsync(db =>
        db.Products.IgnoreQueryFilters().CountAsync(p => ids.Contains(p.Id)));

    /// <summary>Gives one product the trading history that stops it being deleted.</summary>
    private Task PlaceOrderForAsync(Guid productId) => _factory.WithDbAsync(async db =>
    {
        var product = await db.Products.FirstAsync(p => p.Id == productId);
        var customer = await TestData.AddCustomerAsync(db, "09121110099");
        var address = await TestData.AddAddressAsync(db, customer.Id);

        db.Orders.Add(Order.Create(
            OrderNumber.NewOrderNumber(),
            customer.Id,
            [new OrderLineDraft(
                product.Id, product.Slug, product.Title, product.ImageUrl, 1, product.Price)],
            address.Id,
            "تهران، خیابان آزمون",
            "پست پیشتاز",
            "پرداخت در محل",
            "cod",
            subtotal: product.Price,
            discount: Money.Zero,
            shipping: Money.Zero,
            idempotencyKey: $"bulk-{productId}"));

        await db.SaveChangesAsync();
    });

    private sealed record BulkResult(int Changed, IReadOnlyList<string> Blocked);

    private sealed record ProblemBody(string Title, string? Detail);
}
