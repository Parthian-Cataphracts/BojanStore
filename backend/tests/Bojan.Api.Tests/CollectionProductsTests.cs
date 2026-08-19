using System.Net;
using System.Net.Http.Json;
using Bojan.Application.Contracts;
using Bojan.Domain.Admin;
using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// What a collection holds, edited from the collection's own screen.
/// </summary>
/// <remarks>
/// Membership was writable only from the product form, which appends a product
/// to whatever it joins. That leaves an editor no way to say which product
/// leads a curated grouping — which is most of what curating one means — so
/// the collection now posts its whole list, in order.
/// </remarks>
public sealed class CollectionProductsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _collectionId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);

            var collection = new Collection { Slug = "desk", Title = "میز کار" };
            db.Collections.Add(collection);
            await db.SaveChangesAsync();
            _collectionId = collection.Id;

            foreach (var slug in new[] { "pen", "pencil", "ruler" })
            {
                await TestData.AddProductAsync(db, brandId, categoryId, slug, 50_000, stock: 5);
            }

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "collection-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task The_order_posted_is_the_order_stored()
    {
        (await SaveAsync("ruler", "pen", "pencil")).EnsureSuccessStatusCode();

        Assert.Equal(["ruler", "pen", "pencil"], await MembershipAsync());
    }

    [Fact]
    public async Task Reordering_the_same_products_is_not_a_conflict()
    {
        (await SaveAsync("pen", "pencil", "ruler")).EnsureSuccessStatusCode();

        // Every row survives this save and only its position changes — the case
        // that a clear-and-rebuild would turn into a delete and an identical
        // insert, which the unique index refuses when they land the wrong way
        // round.
        (await SaveAsync("ruler", "pencil", "pen")).EnsureSuccessStatusCode();

        Assert.Equal(["ruler", "pencil", "pen"], await MembershipAsync());
    }

    [Fact]
    public async Task A_product_left_out_is_taken_out()
    {
        (await SaveAsync("pen", "pencil", "ruler")).EnsureSuccessStatusCode();
        (await SaveAsync("pen", "ruler")).EnsureSuccessStatusCode();

        Assert.Equal(["pen", "ruler"], await MembershipAsync());
    }

    [Fact]
    public async Task An_empty_list_empties_the_collection()
    {
        (await SaveAsync("pen")).EnsureSuccessStatusCode();
        (await SaveAsync()).EnsureSuccessStatusCode();

        Assert.Empty(await MembershipAsync());
    }

    [Fact]
    public async Task A_product_that_names_nothing_refuses_the_whole_save()
    {
        (await SaveAsync("pen")).EnsureSuccessStatusCode();

        var response = await SaveAsync("pencil", "no-such-product");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["pen"], await MembershipAsync());
    }

    [Fact]
    public async Task The_panel_reads_the_order_back()
    {
        (await SaveAsync("ruler", "pen")).EnsureSuccessStatusCode();

        var collection = await _client.GetFromJsonAsync<AdminCollectionDto>(
            $"/api/admin/collections/{_collectionId}");

        Assert.NotNull(collection);
        Assert.Equal(["ruler", "pen"], collection.ProductSlugs);
        Assert.Equal(2, collection.ProductCount);
    }

    [Fact]
    public async Task What_the_product_form_puts_in_shows_up_here_too()
    {
        // The other direction: the product's own form appends to whatever it
        // joins, and the collection screen has to see that membership.
        var product = await _factory.WithDbAsync(async db => await db.Products.AsNoTracking()
            .Where(p => p.Slug == "pencil")
            .Select(p => p.Id)
            .FirstAsync());

        await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = product.ToString(), collections = new[] { "desk" } });

        Assert.Equal(["pencil"], await MembershipAsync());
    }

    [Fact]
    public async Task Products_may_be_named_by_id_as_well_as_by_slug()
    {
        // The panel posts slugs; an import posts ids, and one request may carry
        // both. They are read in a single statement now, which is the part with
        // something to get wrong: the rows come back unordered and have to be
        // put back into the order they were asked for.
        var pencilId = await IdOfAsync("pencil");

        var response = await _client.PostAsJsonAsync(
            "/api/admin/collections/products",
            new { id = _collectionId.ToString(), products = new[] { pencilId.ToString(), "pen" } });

        response.EnsureSuccessStatusCode();

        Assert.Equal(["pencil", "pen"], await MembershipAsync());
    }

    [Fact]
    public async Task An_id_that_names_nothing_refuses_the_save()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/collections/products",
            new { id = _collectionId.ToString(), products = new[] { "pen", Guid.NewGuid().ToString() } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await MembershipAsync());
    }

    [Fact]
    public async Task The_same_product_named_twice_is_one_membership()
    {
        (await SaveAsync("pen", "pen", "ruler")).EnsureSuccessStatusCode();

        Assert.Equal(["pen", "ruler"], await MembershipAsync());
    }

    private Task<Guid> IdOfAsync(string slug) =>
        _factory.WithDbAsync(async db => await db.Products.AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => p.Id)
            .FirstAsync());

    private Task<HttpResponseMessage> SaveAsync(params string[] products) =>
        _client.PostAsJsonAsync(
            "/api/admin/collections/products",
            new { id = _collectionId.ToString(), products });

    /// <summary>The collection's products, in stored order.</summary>
    private Task<List<string>> MembershipAsync() =>
        _factory.WithDbAsync(async db => await db.CollectionProducts.AsNoTracking()
            .Where(membership => membership.CollectionId == _collectionId)
            .OrderBy(membership => membership.SortOrder)
            .Join(db.Products.AsNoTracking(), m => m.ProductId, p => p.Id, (_, p) => p.Slug)
            .ToListAsync());
}
