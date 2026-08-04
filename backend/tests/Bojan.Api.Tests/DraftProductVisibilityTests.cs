using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// What the public catalogue says about a product that is not published yet.
/// </summary>
/// <remarks>
/// <para>
/// The answer has to be "nothing", on every route that touches it. The global
/// query filter takes archived rows out of every query, but it says nothing
/// about drafts — those are an <c>IsPublished</c> flag each read has to check
/// for itself, and the two variant routes were joining on the slug alone. An
/// unreleased product's options, its per-variant prices and its stock counts
/// were served to anyone who could guess the slug, while the product page it
/// belongs to correctly returned a 404.
/// </para>
/// <para>
/// A slug is guessable by design — it is built from the title — so "you would
/// have to know the URL" is not a control. Pricing ahead of an announcement is
/// exactly the sort of thing a competitor would ask for.
/// </para>
/// </remarks>
public sealed class DraftProductVisibilityTests : IAsyncLifetime, IDisposable
{
    private const string DraftSlug = "unreleased-notebook";
    private const string LiveSlug = "p-live";

    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);

            var draft = await TestData.AddProductAsync(
                db, brandId, categoryId, DraftSlug, 900_000, stock: 7, published: false);

            var live = await TestData.AddProductAsync(
                db, brandId, categoryId, LiveSlug, 300_000, stock: 4);

            await TestData.AddSkuAsync(db, draft.Id, $"{DraftSlug}-a5", price: 900_000, stock: 7);
            await TestData.AddSkuAsync(db, live.Id, $"{LiveSlug}-a5", price: 300_000, stock: 4);

            foreach (var product in new[] { draft, live })
            {
                var axis = new ProductVariantAxis
                {
                    ProductId = product.Id,
                    Key = "size",
                    Label = "اندازه",
                    Kind = VariantAxisKind.Chip,
                    SortOrder = 0,
                };

                db.ProductVariantAxes.Add(axis);
                db.ProductVariantOptions.Add(new ProductVariantOption
                {
                    AxisId = axis.Id,
                    Key = "a5",
                    Label = "A5",
                    SortOrder = 0,
                });
            }

            await db.SaveChangesAsync();
        });

        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task A_draft_products_page_is_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/products/{DraftSlug}")).StatusCode);
    }

    /// <summary>Its options are not a way around that.</summary>
    [Fact]
    public async Task A_draft_products_variants_are_empty()
    {
        var response = await _client.GetAsync($"/api/products/{DraftSlug}/variants");
        response.EnsureSuccessStatusCode();

        Assert.Empty(await response.Content.ReadFromJsonAsync<JsonElement>() is var body && body.ValueKind == JsonValueKind.Array
            ? body.EnumerateArray().ToList()
            : []);
    }

    /// <summary>Nor are its prices and stock counts.</summary>
    [Fact]
    public async Task A_draft_products_skus_are_empty()
    {
        var response = await _client.GetAsync($"/api/products/{DraftSlug}/skus");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.EnumerateArray().ToList());
    }

    /// <summary>
    /// The same two routes still answer for a published product — the filter
    /// has to hide drafts, not empty the catalogue.
    /// </summary>
    [Fact]
    public async Task A_published_products_variants_and_skus_are_served()
    {
        var variants = await _client.GetAsync($"/api/products/{LiveSlug}/variants");
        variants.EnsureSuccessStatusCode();
        Assert.NotEmpty((await variants.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList());

        var skus = await _client.GetAsync($"/api/products/{LiveSlug}/skus");
        skus.EnsureSuccessStatusCode();
        Assert.NotEmpty((await skus.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList());
    }

    /// <summary>A draft product does not appear in the listing either.</summary>
    [Fact]
    public async Task A_draft_product_is_not_in_the_catalogue_listing()
    {
        var response = await _client.GetAsync("/api/products");
        response.EnsureSuccessStatusCode();

        var slugs = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString())
            .ToList();

        Assert.DoesNotContain(DraftSlug, slugs);
        Assert.Contains(LiveSlug, slugs);
    }
}
