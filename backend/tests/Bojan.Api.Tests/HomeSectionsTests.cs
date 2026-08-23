using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Reviews;
using Bojan.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// The switches behind the home page's editorial sections, and the fixtures
/// that fill them on a developer's machine.
/// </summary>
/// <remarks>
/// Two things here are worth pinning down. The switches default to on, because
/// a missing settings row must not read as "off" — every shop has no rows on
/// day one, and reading absence as off would hide three sections nobody chose
/// to hide. And the seeded reviews exist only where the development sign-in
/// does: seeding invented testimonials into a live shop would put fabricated
/// quotes in front of real buyers, under names nobody ever wrote.
/// </remarks>
public sealed class HomeSectionsTests : IDisposable
{
    private readonly BojanApiFactory _factory;

    public HomeSectionsTests()
    {
        _factory = new BojanApiFactory();
        _factory.EnsureDatabaseCreated();
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(string? developmentCustomerPhone = null)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueSeeder>()
            .SeedAsync(null, null, developmentCustomerPhone);
    }

    private async Task<JsonElement> HomeSectionsAsync()
    {
        var settings = await _factory.CreateClient()
            .GetFromJsonAsync<JsonElement>("/api/store/settings");
        return settings.GetProperty("homeSections");
    }

    [Fact]
    public async Task An_unconfigured_shop_shows_all_three_sections()
    {
        var sections = await HomeSectionsAsync();

        Assert.True(sections.GetProperty("testimonials").GetBoolean());
        Assert.True(sections.GetProperty("articles").GetBoolean());
        Assert.True(sections.GetProperty("faq").GetBoolean());
    }

    [Fact]
    public async Task An_owner_can_switch_a_section_off()
    {
        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "sections-owner@bojan.test")).Id);

        var saved = await _factory.CreateAdminClient(ownerId).PostAsJsonAsync(
            "/api/admin/settings",
            new { section = "store", values = new { homeTestimonials = "false" } });
        saved.EnsureSuccessStatusCode();

        var sections = await HomeSectionsAsync();

        Assert.False(sections.GetProperty("testimonials").GetBoolean());
        // The other two are untouched — one switch is one section.
        Assert.True(sections.GetProperty("articles").GetBoolean());
        Assert.True(sections.GetProperty("faq").GetBoolean());
    }

    /// <remarks>
    /// The guard that matters most in this file. A shop's front page quoting
    /// customers who do not exist is not a placeholder somebody notices and
    /// replaces; it is the shop making a claim on behalf of a person who never
    /// made it.
    /// </remarks>
    [Fact]
    public async Task Seeding_a_production_shop_writes_no_reviews()
    {
        await SeedAsync(developmentCustomerPhone: null);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, await db.ProductReviews.CountAsync()));

        var rail = await _factory.CreateClient()
            .GetFromJsonAsync<JsonElement[]>("/api/testimonials");
        Assert.Empty(rail!);
    }

    [Fact]
    public async Task Seeding_with_the_development_sign_in_fills_the_rail_and_the_queue()
    {
        await SeedAsync(developmentCustomerPhone: "09121112233");

        await _factory.WithDbAsync(async db =>
        {
            var reviews = await db.ProductReviews.AsNoTracking().ToListAsync();
            Assert.Equal(3, reviews.Count);

            // One is left pending on purpose: a moderation queue that is empty
            // on first run is a screen a developer cannot tell from a broken one.
            Assert.Contains(reviews, r => r.Status == ModerationStatus.Pending);
            Assert.Equal(2, reviews.Count(r => r.Status == ModerationStatus.Published && r.IsFeaturedOnHome));

            // Every fixture review is about a different product — the unique
            // index on (customer, product) would refuse a second otherwise.
            Assert.Equal(3, reviews.Select(r => r.ProductId).Distinct().Count());
        });

        var rail = await _factory.CreateClient()
            .GetFromJsonAsync<JsonElement[]>("/api/testimonials");
        Assert.Equal(2, rail!.Length);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_fixture_reviews()
    {
        await SeedAsync(developmentCustomerPhone: "09121112233");
        await SeedAsync(developmentCustomerPhone: "09121112233");

        await _factory.WithDbAsync(async db =>
            Assert.Equal(3, await db.ProductReviews.CountAsync()));
    }
}
