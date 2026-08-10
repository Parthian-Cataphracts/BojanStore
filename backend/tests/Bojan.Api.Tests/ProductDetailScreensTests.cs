using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Screens 106-108 — attributes, variants and SKUs.
/// </summary>
/// <remarks>
/// All three save the same way: the product's whole list is posted and the API
/// replaces what it holds, because each screen edits a table in place and a
/// deletion has to delete. These cover that replacement, the identity rules
/// that keep a combination unambiguous, and the role gate.
/// </remarks>
public sealed class ProductDetailScreensTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _productId;
    private Guid _otherProductId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid adminId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 300_000, stock: 5);
            var other = await TestData.AddProductAsync(db, brandId, categoryId, "p-02", 400_000, stock: 5);
            var admin = await TestData.AddAdminAsync(db, AdminRole.Product, "product@example.com");

            _productId = product.Id;
            _otherProductId = other.Id;
            adminId = admin.Id;
        });

        _client = _factory.CreateAdminClient(adminId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    // --- variants -----------------------------------------------------------

    private object Axes(params object[] axes) => new { id = _productId.ToString(), axes };

    [Fact]
    public async Task Variant_axes_round_trip_through_save_and_read()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/variants", Axes(
            new
            {
                key = "color",
                label = "رنگ",
                kind = "swatch",
                options = new[]
                {
                    new { key = "cream", label = "کرمی گرم", hex = "#EFE3D0", available = true },
                    new { key = "teal", label = "سبزآبی", hex = "#0F6F6C", available = false },
                },
            }));

        response.EnsureSuccessStatusCode();

        var read = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/admin/products/{_productId}/variants");

        var axis = read.EnumerateArray().Single();
        Assert.Equal("color", axis.GetProperty("key").GetString());
        Assert.Equal("swatch", axis.GetProperty("kind").GetString());

        var options = axis.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal(2, options.Count);
        Assert.Equal("#EFE3D0", options[0].GetProperty("hex").GetString());
        Assert.False(options[1].GetProperty("available").GetBoolean());
    }

    /// <summary>
    /// The save replaces rather than merges — otherwise a removed axis would
    /// survive every save that no longer mentions it.
    /// </summary>
    [Fact]
    public async Task Saving_variants_replaces_what_was_there()
    {
        await _client.PostAsJsonAsync("/api/admin/products/variants", Axes(
            new { key = "color", label = "رنگ", kind = "chip", options = new[] { new { key = "cream", label = "کرمی" } } },
            new { key = "size", label = "سایز", kind = "chip", options = new[] { new { key = "a5", label = "A5" } } }));

        var second = await _client.PostAsJsonAsync("/api/admin/products/variants", Axes(
            new { key = "size", label = "سایز", kind = "chip", options = new[] { new { key = "a4", label = "A4" } } }));

        second.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var axes = await db.ProductVariantAxes.Include(a => a.Options).ToListAsync();
            Assert.Equal("size", Assert.Single(axes).Key);
            Assert.Equal("a4", Assert.Single(axes[0].Options).Key);

            // The options of the removed axis went with it rather than being
            // left behind pointing at nothing.
            Assert.Equal(1, await db.ProductVariantOptions.CountAsync());
        });
    }

    [Fact]
    public async Task Two_axes_with_one_key_are_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/variants", Axes(
            new { key = "color", label = "رنگ", kind = "chip", options = new[] { new { key = "a", label = "الف" } } },
            new { key = "color", label = "رنگ دیگر", kind = "chip", options = new[] { new { key = "b", label = "ب" } } }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.WithDbAsync(async db => Assert.False(await db.ProductVariantAxes.AnyAsync()));
    }

    /// <summary>A swatch draws a colour dot, so an option on one needs a colour.</summary>
    [Fact]
    public async Task A_swatch_option_without_a_colour_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/variants", Axes(
            new { key = "color", label = "رنگ", kind = "swatch", options = new[] { new { key = "cream", label = "کرمی" } } }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_axis_with_no_options_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/variants", Axes(
            new { key = "color", label = "رنگ", kind = "chip", options = Array.Empty<object>() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- SKUs ---------------------------------------------------------------

    [Fact]
    public async Task Skus_round_trip_and_default_their_price_to_the_product()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/skus", new
        {
            id = _productId.ToString(),
            skus = new object[]
            {
                new { code = "bz-a5-crm", barcode = "6260100234561", combination = "cream|a5", stock = 24, price = 350_000 },
                // No price: the product's own is the sensible default rather
                // than zero, which would put a free unit in the catalogue.
                new { code = "BZ-A4-CRM", combination = "cream|a4", stock = 8 },
            },
        });

        response.EnsureSuccessStatusCode();

        var read = await _client.GetFromJsonAsync<JsonElement>($"/api/admin/products/{_productId}/skus");
        var rows = read.EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);
        // Codes are normalised, so the same code in either case is the same code.
        Assert.Contains(rows, row => row.GetProperty("code").GetString() == "BZ-A5-CRM");
        Assert.Equal(300_000, rows.Single(row => row.GetProperty("code").GetString() == "BZ-A4-CRM")
            .GetProperty("price").GetInt64());
    }

    [Fact]
    public async Task A_code_another_product_already_uses_is_refused()
    {
        var first = await _client.PostAsJsonAsync("/api/admin/products/skus", new
        {
            id = _otherProductId.ToString(),
            skus = new[] { new { code = "SHARED-01" } },
        });
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/admin/products/skus", new
        {
            id = _productId.ToString(),
            skus = new[] { new { code = "SHARED-01" } },
        });

        // A field error the form can point at, not the 500 the unique index
        // would otherwise produce.
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task The_same_code_twice_in_one_save_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/skus", new
        {
            id = _productId.ToString(),
            skus = new[] { new { code = "DUP-01" }, new { code = "dup-01" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.WithDbAsync(async db => Assert.False(await db.ProductSkus.AnyAsync()));
    }

    [Fact]
    public async Task Negative_stock_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/skus", new
        {
            id = _productId.ToString(),
            skus = new[] { new { code = "NEG-01", stock = -1 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- attributes ---------------------------------------------------------

    [Fact]
    public async Task Attributes_round_trip_with_their_values_intact()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/attributes", new
        {
            id = _productId.ToString(),
            attributes = new[]
            {
                new
                {
                    name = "گرماژ",
                    kind = "number",
                    // The middle value carries a comma, which is ordinary
                    // Persian punctuation — it must survive as one value.
                    values = new[] { "۷۰", "۸۰، ۹۰", "۱۰۰" },
                    filterable = true,
                },
            },
        });

        response.EnsureSuccessStatusCode();

        var read = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/admin/products/{_productId}/attributes");

        var attribute = read.EnumerateArray().Single();
        Assert.Equal("number", attribute.GetProperty("kind").GetString());
        Assert.True(attribute.GetProperty("filterable").GetBoolean());

        var values = attribute.GetProperty("values").EnumerateArray()
            .Select(value => value.GetString()).ToList();

        Assert.Equal(["۷۰", "۸۰، ۹۰", "۱۰۰"], values);
    }

    [Fact]
    public async Task An_unknown_attribute_kind_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/attributes", new
        {
            id = _productId.ToString(),
            attributes = new[] { new { name = "جنس", kind = "colour" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- validation reaches the panel's writes -------------------------------

    /// <summary>
    /// A settings value longer than its column is a field error, not a 500.
    /// </summary>
    /// <remarks>
    /// None of the panel's writes ran a validator, so every bound the database
    /// declares was enforced only by the database. The group filter is what
    /// covers all of them at once; this is the case that proves it is wired.
    /// </remarks>
    [Fact]
    public async Task An_over_long_setting_value_is_refused_before_the_database_sees_it()
    {
        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@example.com")).Id);

        using var owner = _factory.CreateAdminClient(ownerId);

        var response = await owner.PostAsJsonAsync("/api/admin/settings", new
        {
            section = "store",
            values = new Dictionary<string, string> { ["name"] = new string('x', 8_001) },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The row this request would have written, not the whole table: the
        // host seeds its own payment settings so the money path is reachable,
        // and asserting the table is empty made this test about that instead of
        // about the value it rejected.
        await _factory.WithDbAsync(async db =>
            Assert.False(await db.Settings.AnyAsync(s => s.Section == "store" && s.Key == "name")));
    }

    [Fact]
    public async Task A_settings_save_within_its_bounds_still_goes_through()
    {
        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner2@example.com")).Id);

        using var owner = _factory.CreateAdminClient(ownerId);

        var response = await owner.PostAsJsonAsync("/api/admin/settings", new
        {
            section = "store",
            values = new Dictionary<string, string> { ["name"] = "فروشگاه بوژان" },
        });

        response.EnsureSuccessStatusCode();
        await _factory.WithDbAsync(async db => Assert.True(await db.Settings.AnyAsync()));
    }

    /// <summary>
    /// A discount that ends before it starts never applies.
    /// </summary>
    /// <remarks>
    /// Nothing checked this at any layer: the service takes both dates as
    /// given, so the save succeeded and the discount simply never showed up,
    /// with nothing on the screen or in the record to say why.
    /// </remarks>
    [Fact]
    public async Task A_discount_window_that_ends_before_it_starts_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/discount", new
        {
            id = _productId.ToString(),
            percent = 10,
            startsAt = "2026-09-01T00:00:00Z",
            endsAt = "2026-08-01T00:00:00Z",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_discount_window_in_order_is_accepted()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products/discount", new
        {
            id = _productId.ToString(),
            percent = 10,
            startsAt = "2026-08-01T00:00:00Z",
            endsAt = "2026-09-01T00:00:00Z",
        });

        response.EnsureSuccessStatusCode();
    }

    // --- the role gate ------------------------------------------------------

    /// <summary>
    /// These are catalogue writes, so support may not make them — the same gate
    /// every other product write sits behind.
    /// </summary>
    [Fact]
    public async Task An_operator_without_the_catalogue_role_is_refused()
    {
        Guid supportId = default;
        await _factory.WithDbAsync(async db =>
            supportId = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@example.com")).Id);

        using var support = _factory.CreateAdminClient(supportId);

        foreach (var path in new[] { "variants", "skus", "attributes" })
        {
            var response = await support.PostAsJsonAsync(
                $"/api/admin/products/{path}", new { id = _productId.ToString() });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
