using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The panel's writes: what they refuse, and what they actually store.
/// </summary>
/// <remarks>
/// <para>
/// Two failures these cover, both of which looked like success from the panel.
/// A body longer than its column reached the database and came back as a 500
/// rather than the field error the form can point at — the group's validation
/// filter was in place but only five of the twenty-three request shapes had a
/// validator for it to find. And a field the form collects but the request
/// record does not declare is dropped by the deserialiser, so the operator
/// filled it, the panel forwarded it, and the API answered 200 having saved
/// nothing.
/// </para>
/// <para>
/// The image checks are the security half: every one of these URLs is rendered
/// to every visitor, so a field that takes an arbitrary URL points the shop at
/// whatever host the caller names.
/// </para>
/// </remarks>
public sealed class AdminWriteHardeningTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "hardening", 100_000, stock: 5);
            _productId = product.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "hardening-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    // --- validation ---------------------------------------------------------

    [Fact]
    public async Task A_title_longer_than_its_column_is_a_field_error_not_a_server_fault()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), title = new string('ط', 400) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_support_reply_longer_than_its_column_is_a_field_error()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/support/replies",
            new { threadId = Guid.NewGuid().ToString(), body = new string('ط', 9000) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_stock_movement_quantity_beyond_the_sane_ceiling_is_refused()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/inventory/movements",
            new { productId = _productId.ToString(), kind = "adjust", quantity = 50_000_000, reason = "stocktake" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The list ceilings are the ones that turn one request into thousands of
    /// inserts if they are missing.
    /// </summary>
    [Fact]
    public async Task An_unbounded_attribute_list_is_refused()
    {
        var attributes = Enumerable.Range(0, 500)
            .Select(index => new { name = $"attr-{index}", kind = "text", values = new[] { "x" } })
            .ToArray();

        var response = await _client.PostAsJsonAsync(
            "/api/admin/products/attributes",
            new { id = _productId.ToString(), attributes });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- fields that were being dropped -------------------------------------

    [Fact]
    public async Task Every_field_the_product_form_collects_is_stored()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products", new
        {
            id = _productId.ToString(),
            compareAt = 180_000,
            lowStock = 42,
            trackStock = false,
            backorder = true,
            metaTitle = "عنوان متا",
            metaDescription = "توضیحات متا",
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.Products.AsNoTracking().FirstAsync(p => p.Id == _productId);

            Assert.Equal(180_000, stored.CompareAtPrice!.Value.Amount);
            Assert.Equal(42, stored.LowStockThreshold);
            Assert.False(stored.TrackStock);
            Assert.True(stored.AllowBackorder);
            Assert.Equal("عنوان متا", stored.MetaTitle);
            Assert.Equal("توضیحات متا", stored.MetaDescription);
        });
    }

    [Fact]
    public async Task Every_field_the_brand_form_collects_is_stored()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/brands", new
        {
            title = "برند تازه",
            tagline = "شعار برند",
            country = "ایران",
            metaTitle = "متای برند",
            metaDescription = "توضیح متای برند",
            featured = true,
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.Brands.AsNoTracking().FirstAsync(b => b.Name == "برند تازه");

            Assert.Equal("شعار برند", stored.Tagline);
            Assert.Equal("ایران", stored.Country);
            Assert.Equal("متای برند", stored.MetaTitle);
            Assert.Equal("توضیح متای برند", stored.MetaDescription);
            Assert.True(stored.IsFeatured);
        });
    }

    [Fact]
    public async Task Every_field_the_category_form_collects_is_stored()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/categories", new
        {
            title = "دسته تازه",
            metaTitle = "متای دسته",
            metaDescription = "توضیح متای دسته",
            showInMenu = false,
            order = 7,
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.Categories.AsNoTracking().FirstAsync(c => c.Name == "دسته تازه");

            Assert.Equal("متای دسته", stored.MetaTitle);
            Assert.Equal("توضیح متای دسته", stored.MetaDescription);
            Assert.False(stored.ShowInMenu);
            Assert.Equal(7, stored.SortOrder);
        });
    }

    [Fact]
    public async Task Every_field_the_collection_form_collects_is_stored()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/collections", new
        {
            title = "مجموعه تازه",
            summary = "خلاصه مجموعه",
            editorialNote = "یادداشت سردبیر",
            featured = true,
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.Collections.AsNoTracking().FirstAsync(c => c.Title == "مجموعه تازه");

            Assert.Equal("خلاصه مجموعه", stored.Summary);
            Assert.Equal("یادداشت سردبیر", stored.EditorialNote);
            Assert.True(stored.IsFeatured);
        });
    }

    [Fact]
    public async Task A_product_saved_with_a_slug_keeps_it_and_reads_it_back()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), slug = "a-chosen-slug" });

        response.EnsureSuccessStatusCode();

        var read = await _client.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(
            $"/api/admin/products/{_productId}");

        Assert.Equal("a-chosen-slug", read!["slug"].GetString());
    }

    // --- image fields -------------------------------------------------------

    [Theory]
    [InlineData("/api/admin/brands", "logo")]
    [InlineData("/api/admin/collections", "cover")]
    [InlineData("/api/admin/content", "cover")]
    public async Task An_image_url_this_api_did_not_issue_is_refused(string path, string field)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = "off-site image",
            [field] = "https://evil.example/tracker.png",
        };

        if (path.EndsWith("/content", StringComparison.Ordinal))
        {
            body["kind"] = "page";
        }

        var response = await _client.PostAsJsonAsync(path, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- coupon integrity ---------------------------------------------------

    /// <summary>
    /// An order records the coupon code as text, and the "already used" check
    /// matches on that text — so renaming a redeemed coupon would hand every
    /// customer who used it a second use.
    /// </summary>
    [Fact]
    public async Task A_redeemed_coupon_cannot_be_renamed()
    {
        Guid couponId = default;

        await _factory.WithDbAsync(async db =>
        {
            var coupon = new Domain.Orders.Coupon { Code = "REDEEMED10", PercentOff = 10 };
            coupon.RecordRedemption();
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync();
            couponId = coupon.Id;
        });

        var response = await _client.PostAsJsonAsync(
            "/api/admin/coupons",
            new { id = couponId.ToString(), code = "RENAMED10" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Renaming_a_coupon_onto_an_existing_code_is_a_conflict_not_a_server_fault()
    {
        Guid couponId = default;

        await _factory.WithDbAsync(async db =>
        {
            db.Coupons.Add(new Domain.Orders.Coupon { Code = "TAKEN20", PercentOff = 20 });
            var moving = new Domain.Orders.Coupon { Code = "MOVING20", PercentOff = 20 };
            db.Coupons.Add(moving);
            await db.SaveChangesAsync();
            couponId = moving.Id;
        });

        var response = await _client.PostAsJsonAsync(
            "/api/admin/coupons",
            new { id = couponId.ToString(), code = "TAKEN20" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- reads --------------------------------------------------------------

    /// <summary>
    /// The detail read used to page the whole customer list and search it in
    /// memory, so any customer outside the newest page answered 404 on a screen
    /// reached from their own row.
    /// </summary>
    [Fact]
    public async Task A_customer_beyond_the_first_page_is_still_found_by_id()
    {
        Guid oldestId = default;

        await _factory.WithDbAsync(async db =>
        {
            var created = DateTimeOffset.UtcNow.AddYears(-2);

            // The list is ordered newest first and pages at MaxPageSize; this
            // one is deliberately older than every other row.
            var oldest = new Domain.Customers.Customer
            {
                Phone = "09120000001",
                FirstName = "قدیمی",
                LastName = "ترین",
                CreatedAtUtc = created,
            };
            db.Customers.Add(oldest);

            for (var index = 0; index < 25; index++)
            {
                db.Customers.Add(new Domain.Customers.Customer
                {
                    Phone = $"091211{index:D5}",
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-index),
                });
            }

            await db.SaveChangesAsync();
            oldestId = oldest.Id;
        });

        var response = await _client.GetAsync($"/api/admin/customers/{oldestId}");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        Assert.Equal(oldestId.ToString(), body!["id"].GetString());
    }
}

/// <summary>
/// Stock flags decide whether the checkout may refuse a basket, so they are
/// checked where that decision is made.
/// </summary>
public sealed class StockFlagTests
{
    [Fact]
    public void An_untracked_product_is_never_short()
    {
        var product = Untracked(trackStock: false, allowBackorder: false);

        Assert.False(product.RequiresStockOnHand);

        // The count is not kept, so selling does not move it.
        product.ReduceStock(100);
        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void A_backorder_product_may_go_past_its_stock()
    {
        var product = Untracked(trackStock: true, allowBackorder: true);
        product.Stock = 2;

        Assert.False(product.RequiresStockOnHand);

        product.ReduceStock(5);

        // Negative on purpose: it records what is owed once a delivery lands.
        Assert.Equal(-3, product.Stock);
    }

    [Fact]
    public void A_tracked_product_without_backorder_still_refuses()
    {
        var product = Untracked(trackStock: true, allowBackorder: false);
        product.Stock = 2;

        Assert.True(product.RequiresStockOnHand);
        Assert.Throws<InvalidOperationException>(() => product.ReduceStock(5));
    }

    private static Product Untracked(bool trackStock, bool allowBackorder) => new()
    {
        Slug = "flagged",
        Title = "محصول",
        BrandId = Guid.NewGuid(),
        CategoryId = Guid.NewGuid(),
        Price = new Money(1000),
        ImageUrl = "https://example.test/flagged.jpg",
        TrackStock = trackStock,
        AllowBackorder = allowBackorder,
    };
}
