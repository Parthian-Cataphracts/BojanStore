using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// Verifies the seeder against the design's own fixture file.
/// </summary>
/// <remarks>
/// This is the test that would catch the seed data drifting from the frontend
/// fixtures it was lifted from: the embedded JSON is deserialised into the
/// seeder's own records, so a renamed or retyped field fails here rather than
/// at the first <c>dotnet run</c> against an empty database.
/// </remarks>
public sealed class CatalogueSeederTests : IDisposable
{
    private readonly BojanApiFactory _factory;

    public CatalogueSeederTests()
    {
        _factory = new BojanApiFactory();
        _factory.EnsureDatabaseCreated();
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(string? adminPassword = null, string? adminPhone = null)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueSeeder>().SeedAsync(adminPassword, adminPhone);
    }

    [Fact]
    public async Task Seeding_loads_the_design_catalogue()
    {
        await SeedAsync();

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(33, await db.Products.CountAsync());
            Assert.Equal(6, await db.Collections.CountAsync());
            Assert.Equal(7, await db.Articles.CountAsync());
            Assert.Equal(3, await db.ShippingMethods.CountAsync());
            Assert.Equal(3, await db.PaymentMethods.CountAsync());

            // Every product resolves to a brand and a category, or it would be
            // invisible to the catalogue's inner joins.
            var orphaned = await db.Products
                .CountAsync(p => !db.Brands.Any(b => b.Id == p.BrandId)
                    || !db.Categories.Any(c => c.Id == p.CategoryId));

            Assert.Equal(0, orphaned);
        });
    }

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        await SeedAsync();
        await SeedAsync();

        await _factory.WithDbAsync(async db => Assert.Equal(33, await db.Products.CountAsync()));
    }

    [Fact]
    public async Task No_operator_is_created_without_a_configured_password()
    {
        await SeedAsync(adminPassword: null);

        await _factory.WithDbAsync(async db => Assert.Equal(0, await db.AdminUsers.CountAsync()));
    }

    [Fact]
    public async Task The_seeded_catalogue_serves_the_storefront()
    {
        await SeedAsync();

        using var client = _factory.CreateClient();

        var listing = await (await client.GetAsync("/api/products?pageSize=5"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(33, listing.GetProperty("total").GetInt32());
        Assert.Equal(5, listing.GetProperty("items").GetArrayLength());

        var categories = await (await client.GetAsync("/api/categories")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(categories.GetArrayLength() >= 5);

        var collections = await (await client.GetAsync("/api/collections")).Content.ReadFromJsonAsync<JsonElement>();
        var first = collections[0];
        // A collection carries its members as slugs, which the storefront then
        // resolves through a second call.
        Assert.True(first.GetProperty("productSlugs").GetArrayLength() > 0);

        var products = await (await client.GetAsync(
                $"/api/collections/{first.GetProperty("slug").GetString()}/products"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(products.GetArrayLength() > 0);
    }

    [Fact]
    public async Task A_seeded_article_keeps_its_typed_body_blocks()
    {
        await SeedAsync();

        using var client = _factory.CreateClient();

        var articles = await (await client.GetAsync("/api/articles")).Content.ReadFromJsonAsync<JsonElement>();
        var slug = articles[0].GetProperty("slug").GetString();

        var article = await (await client.GetAsync($"/api/articles/{slug}")).Content.ReadFromJsonAsync<JsonElement>();
        var body = article.GetProperty("body");

        Assert.True(body.GetArrayLength() > 0);

        foreach (var block in body.EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            Assert.Contains(type, new[] { "paragraph", "heading", "product" });

            // A product block carries no text — the frontend renders the
            // recommended product in its place.
            Assert.Equal(type == "product", !block.TryGetProperty("text", out _));
        }
    }
}
