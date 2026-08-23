using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The review moderation queue and the home page's testimonial rail.
/// </summary>
/// <remarks>
/// Reviews arrived <see cref="ModerationStatus.Pending"/> and the storefront
/// served published ones only, and nothing in the panel could move a review
/// between the two — so every review a customer ever wrote sat unpublished and
/// the shop looked, from the outside, like a shop nobody had reviewed.
///
/// The rail on top of it has a second condition: published <em>and</em>
/// featured. Most of what is worth pinning down here is the interaction of
/// those two flags, because that is where the panel and the storefront can
/// disagree about what the shop is showing.
/// </remarks>
public sealed class ReviewModerationTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _public = null!;

    private Guid _productId;
    private string _productSlug = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "review-owner@bojan.test")).Id;

            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "review-target", 100_000, 5);
            _productId = product.Id;
            _productSlug = product.Slug;
        });

        _admin = _factory.CreateAdminClient(ownerId);
        _public = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_pending_review_is_invisible_until_it_is_approved()
    {
        var id = await AddReviewAsync(ModerationStatus.Pending);

        Assert.Empty(await ProductReviewsAsync());

        var approved = await _admin.PostAsJsonAsync(
            "/api/admin/reviews/status", new { id = id.ToString(), status = "published" });
        approved.EnsureSuccessStatusCode();

        Assert.Single(await ProductReviewsAsync());
    }

    [Fact]
    public async Task The_home_rail_carries_only_reviews_that_are_both_published_and_featured()
    {
        // Published but not featured — a review on its product page, which is
        // not the same as a review the shop quotes on its front door.
        await AddReviewAsync(ModerationStatus.Published);

        Assert.Empty(await TestimonialsAsync());
    }

    [Fact]
    public async Task Featuring_a_review_puts_it_on_the_home_rail_with_the_product_it_is_about()
    {
        var id = await AddReviewAsync(ModerationStatus.Published);

        var featured = await _admin.PostAsJsonAsync(
            "/api/admin/reviews/featured", new { id = id.ToString(), featured = true });
        featured.EnsureSuccessStatusCode();

        var rail = await TestimonialsAsync();
        var quote = Assert.Single(rail);

        // The product travels with the quote. A testimonial the reader cannot
        // trace back to what it praises is a slogan.
        Assert.Equal(_productSlug, quote.GetProperty("productSlug").GetString());
        Assert.False(string.IsNullOrWhiteSpace(quote.GetProperty("productTitle").GetString()));
    }

    /// <remarks>
    /// The failure this guards against is a silent disagreement: the panel
    /// showing the star still lit on a review the storefront has already
    /// stopped serving, so an operator who pulled a review back has no way to
    /// tell whether it worked.
    /// </remarks>
    [Fact]
    public async Task Rejecting_a_featured_review_clears_the_home_flag_as_well()
    {
        var id = await AddReviewAsync(ModerationStatus.Published, featured: true);
        Assert.Single(await TestimonialsAsync());

        var rejected = await _admin.PostAsJsonAsync(
            "/api/admin/reviews/status", new { id = id.ToString(), status = "rejected" });
        rejected.EnsureSuccessStatusCode();

        Assert.Empty(await TestimonialsAsync());

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == id);
            Assert.False(stored.IsFeaturedOnHome);
        });
    }

    /// <remarks>
    /// Refused rather than stored, because the storefront requires both flags:
    /// a tick accepted here would save, light up in the panel, and put nothing
    /// on the home page.
    /// </remarks>
    [Fact]
    public async Task A_review_that_is_not_published_cannot_be_featured()
    {
        var id = await AddReviewAsync(ModerationStatus.Pending);

        var response = await _admin.PostAsJsonAsync(
            "/api/admin/reviews/featured", new { id = id.ToString(), featured = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == id);
            Assert.False(stored.IsFeaturedOnHome);
        });
    }

    [Fact]
    public async Task The_queue_filters_by_moderation_state()
    {
        await AddReviewAsync(ModerationStatus.Pending, phone: "09120000101");
        await AddReviewAsync(ModerationStatus.Published, phone: "09120000102");

        Assert.Single(await QueueAsync("pending"));
        Assert.Single(await QueueAsync("published"));
        Assert.Empty(await QueueAsync("rejected"));
    }

    /// <remarks>
    /// An unknown status is an empty page rather than the unfiltered queue.
    /// Answering a hand-typed address with every review would look exactly like
    /// the filter having silently worked.
    /// </remarks>
    [Fact]
    public async Task An_unknown_status_filter_returns_nothing_rather_than_everything()
    {
        await AddReviewAsync(ModerationStatus.Published);

        Assert.Empty(await QueueAsync("nonsense"));
    }

    /// <remarks>
    /// The panel posts the product's slug alongside the verdict — not for the
    /// API, which has no field for it, but so the write proxy can name the
    /// cached review list it has to drop. The API ignoring it is what makes
    /// that arrangement work; rejecting it would break approving a review
    /// from the panel entirely, and only from the panel.
    /// </remarks>
    [Fact]
    public async Task A_field_the_panel_adds_for_its_own_use_is_ignored_rather_than_refused()
    {
        var id = await AddReviewAsync(ModerationStatus.Pending);

        var response = await _admin.PostAsJsonAsync(
            "/api/admin/reviews/status",
            new { id = id.ToString(), status = "published", slug = _productSlug });

        response.EnsureSuccessStatusCode();
        Assert.Single(await ProductReviewsAsync());
    }

    [Fact]
    public async Task Deleting_a_review_removes_it_outright()
    {
        var id = await AddReviewAsync(ModerationStatus.Published);

        var deleted = await _admin.PostAsJsonAsync("/api/admin/reviews/delete", new { id = id.ToString() });
        deleted.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.False(await db.ProductReviews.AnyAsync(r => r.Id == id)));
    }

    /// <remarks>
    /// The cap is what keeps a public, cacheable route from being turned into
    /// an export of every published review the shop holds.
    /// </remarks>
    [Fact]
    public async Task The_rail_refuses_to_return_more_than_its_cap()
    {
        for (var i = 0; i < 14; i++)
        {
            await AddReviewAsync(ModerationStatus.Published, featured: true, phone: $"091200002{i:D2}");
        }

        var rail = await TestimonialsAsync(limit: 500);
        Assert.Equal(12, rail.Length);
    }

    private async Task<Guid> AddReviewAsync(
        ModerationStatus status,
        bool featured = false,
        string phone = "09120000100")
    {
        var id = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            // A review per customer per product — the unique index refuses a
            // second, so each fixture review needs an author of its own.
            var customer = await TestData.AddCustomerAsync(db, phone);

            var review = new ProductReview
            {
                ProductId = _productId,
                CustomerId = customer.Id,
                AuthorName = "کاربر آزمون",
                Rating = 5,
                Body = "متن نظر آزمایشی.",
                Status = status,
                IsFeaturedOnHome = featured,
                IsVerifiedPurchase = true,
            };

            db.ProductReviews.Add(review);
            await db.SaveChangesAsync();
            id = review.Id;
        });

        return id;
    }

    private async Task<System.Text.Json.JsonElement[]> ProductReviewsAsync() =>
        await _public.GetFromJsonAsync<System.Text.Json.JsonElement[]>(
            $"/api/products/{_productSlug}/reviews") ?? [];

    private async Task<System.Text.Json.JsonElement[]> TestimonialsAsync(int? limit = null) =>
        await _public.GetFromJsonAsync<System.Text.Json.JsonElement[]>(
            limit is null ? "/api/testimonials" : $"/api/testimonials?limit={limit}") ?? [];

    private async Task<System.Text.Json.JsonElement[]> QueueAsync(string status)
    {
        var page = await _admin.GetFromJsonAsync<System.Text.Json.JsonElement>(
            $"/api/admin/reviews?status={status}");
        return [.. page.GetProperty("items").EnumerateArray()];
    }
}
