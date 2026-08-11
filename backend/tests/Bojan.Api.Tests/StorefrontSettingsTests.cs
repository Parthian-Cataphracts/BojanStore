using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Content;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The two reads that make the shop's own words the owner's rather than the
/// developer's.
/// </summary>
/// <remarks>
/// <para>
/// The storefront had the shop's name, address, support number, delivery
/// promise, return window and free-shipping threshold written into its
/// components — several of them in more than one place, quoting different
/// figures. The settings screen saved values nothing outside the panel ever
/// read.
/// </para>
/// <para>
/// The informational pages were worse: the terms a customer agrees to by buying,
/// and the returns policy the shop is held to, were compiled into the bundle.
/// </para>
/// </remarks>
public sealed class StorefrontSettingsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _owner = null!;
    private HttpClient _public = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@example.com")).Id);

        _owner = _factory.CreateAdminClient(ownerId);
        _public = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _owner?.Dispose();
        _public?.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> SaveAsync(object values) =>
        _owner.PostAsJsonAsync("/api/admin/settings", new { section = "store", values });

    private Task<JsonElement> ReadAsync() =>
        _public.GetFromJsonAsync<JsonElement>("/api/store/settings");

    /// <summary>
    /// A shop that has never opened the settings screen looks exactly as the
    /// storefront used to — the fallbacks are the copy that was written into the
    /// components, so nothing renders empty on day one.
    /// </summary>
    [Fact]
    public async Task An_unconfigured_shop_answers_with_what_the_storefront_shipped()
    {
        var settings = await ReadAsync();

        Assert.Equal("بوژان", settings.GetProperty("identity").GetProperty("name").GetString());
        Assert.Equal(
            1_000_000,
            settings.GetProperty("promises").GetProperty("freeShippingThreshold").GetInt64());
        Assert.Equal(7, settings.GetProperty("promises").GetProperty("returnWindowDays").GetInt32());
    }

    [Fact]
    public async Task What_the_owner_saves_is_what_the_storefront_reads()
    {
        (await SaveAsync(new
        {
            storeName = "فروشگاه نمونه",
            tagline = "شعار تازه",
            phone = "۰۲۱-۲۲۲۲۲۲۲۲",
            email = "hello@example.test",
            address = "تهران، خیابان نمونه",
            instagram = "example.shop",
            freeShippingThreshold = "2500000",
            returnWindowDays = "14",
            deliveryEstimate = "۱ تا ۳ روز کاری",
        })).EnsureSuccessStatusCode();

        var settings = await ReadAsync();

        Assert.Equal("فروشگاه نمونه", settings.GetProperty("identity").GetProperty("name").GetString());
        Assert.Equal("شعار تازه", settings.GetProperty("identity").GetProperty("tagline").GetString());
        Assert.Equal("hello@example.test", settings.GetProperty("contact").GetProperty("email").GetString());
        Assert.Equal("example.shop", settings.GetProperty("social").GetProperty("instagram").GetString());

        var promises = settings.GetProperty("promises");
        Assert.Equal(2_500_000, promises.GetProperty("freeShippingThreshold").GetInt64());
        Assert.Equal(14, promises.GetProperty("returnWindowDays").GetInt32());
        Assert.Equal("۱ تا ۳ روز کاری", promises.GetProperty("deliveryEstimate").GetString());
    }

    /// <summary>
    /// Zero is a shop that never gives free delivery, which is a real answer and
    /// not the same as an unset field — so it has to survive the fallback.
    /// </summary>
    [Fact]
    public async Task A_threshold_of_zero_means_no_free_delivery_rather_than_the_default()
    {
        (await SaveAsync(new { freeShippingThreshold = "0" })).EnsureSuccessStatusCode();

        var settings = await ReadAsync();

        Assert.Equal(0, settings.GetProperty("promises").GetProperty("freeShippingThreshold").GetInt64());
    }

    /// <summary>
    /// A figure typed with Persian digits, or with a comma, does not parse. The
    /// fallback is what the shop had rather than zero — quoting free delivery on
    /// every order because someone typed «۱۰۰۰۰۰۰» would cost real money.
    /// </summary>
    [Fact]
    public async Task A_threshold_that_does_not_parse_falls_back_rather_than_reading_as_zero()
    {
        (await SaveAsync(new { freeShippingThreshold = "۲۵۰۰۰۰۰" })).EnsureSuccessStatusCode();

        var settings = await ReadAsync();

        Assert.Equal(
            1_000_000,
            settings.GetProperty("promises").GetProperty("freeShippingThreshold").GetInt64());
    }

    /// <summary>
    /// A shop with one address answers B2B enquiries on it. An empty row on the
    /// consultant screen is worse than the main address repeated.
    /// </summary>
    [Fact]
    public async Task The_business_address_falls_back_to_the_main_one()
    {
        (await SaveAsync(new { email = "hello@example.test" })).EnsureSuccessStatusCode();

        var contact = (await ReadAsync()).GetProperty("contact");

        Assert.Equal("hello@example.test", contact.GetProperty("businessEmail").GetString());
    }

    /// <summary>
    /// Public and unauthenticated: it is the shop's own name and phone number,
    /// rendered on every page, including to visitors who have never signed in.
    /// </summary>
    [Fact]
    public async Task The_settings_are_readable_without_a_credential()
    {
        var response = await _public.GetAsync("/api/store/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- informational pages ------------------------------------------------

    [Fact]
    public async Task A_page_the_shop_has_not_written_is_a_404()
    {
        var response = await _public.GetAsync("/api/pages/terms");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_published_page_is_served_as_the_owner_wrote_it()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ContentEntries.Add(new ContentEntry
            {
                Slug = "terms",
                Title = "قوانین فروشگاه",
                Kind = ContentKind.Page,
                Body = "مقدمه\n\n## ثبت سفارش\n\nمتن",
                Status = ContentStatus.Published,
            });

            await db.SaveChangesAsync();
        });

        var page = await _public.GetFromJsonAsync<JsonElement>("/api/pages/terms");

        Assert.Equal("قوانین فروشگاه", page.GetProperty("title").GetString());
        Assert.Contains("ثبت سفارش", page.GetProperty("body").GetString());
    }

    /// <summary>
    /// A draft is a page somebody is still writing. Serving it would publish
    /// half-finished terms to every customer.
    /// </summary>
    [Fact]
    public async Task A_draft_page_is_not_served()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ContentEntries.Add(new ContentEntry
            {
                Slug = "privacy",
                Title = "حریم خصوصی",
                Kind = ContentKind.Page,
                Body = "هنوز تمام نشده",
                Status = ContentStatus.Draft,
            });

            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound, (await _public.GetAsync("/api/pages/privacy")).StatusCode);
    }

    /// <summary>
    /// An article and a page can share a slug without either becoming the other:
    /// this route serves pages, and a magazine post that happens to be called
    /// "shipping" is not the shipping policy.
    /// </summary>
    [Fact]
    public async Task An_entry_of_another_kind_is_not_served_as_a_page()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ContentEntries.Add(new ContentEntry
            {
                Slug = "shipping",
                Title = "یادداشتی درباره ارسال",
                Kind = ContentKind.Article,
                Body = "متن مجله",
                Status = ContentStatus.Published,
            });

            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound, (await _public.GetAsync("/api/pages/shipping")).StatusCode);
    }

    // --- banners ------------------------------------------------------------

    /// <summary>
    /// The heading over the shop's largest picture. It was written into the home
    /// page component, while the panel had a banner editor whose rows nothing
    /// read.
    /// </summary>
    [Fact]
    public async Task A_published_banner_is_served_by_slug()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ContentEntries.Add(new ContentEntry
            {
                Slug = "home-hero",
                Title = "فروش پاییزه",
                Kind = ContentKind.Banner,
                Excerpt = "تا سی درصد تخفیف",
                CoverUrl = "https://cdn.example.test/hero.jpg",
                Status = ContentStatus.Published,
            });

            await db.SaveChangesAsync();
        });

        var banner = await _public.GetFromJsonAsync<JsonElement>("/api/banners/home-hero");

        Assert.Equal("فروش پاییزه", banner.GetProperty("title").GetString());
        Assert.Equal("تا سی درصد تخفیف", banner.GetProperty("subtitle").GetString());
        Assert.Equal("https://cdn.example.test/hero.jpg", banner.GetProperty("imageUrl").GetString());
    }

    /// <summary>
    /// A banner with no picture is one somebody started and did not finish. The
    /// hero is a photograph with words over it, and half of it is worse than the
    /// one the storefront ships with.
    /// </summary>
    [Fact]
    public async Task A_banner_with_no_image_is_not_served()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ContentEntries.Add(new ContentEntry
            {
                Slug = "home-hero",
                Title = "بدون تصویر",
                Kind = ContentKind.Banner,
                Status = ContentStatus.Published,
            });

            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound, (await _public.GetAsync("/api/banners/home-hero")).StatusCode);
    }

    [Fact]
    public async Task A_shop_with_no_banner_is_a_404_so_the_storefront_keeps_its_own()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _public.GetAsync("/api/banners/home-hero")).StatusCode);
    }

    // --- the FAQ ------------------------------------------------------------

    /// <summary>
    /// The panel has had an FAQ editor since screen 125 and nothing read what it
    /// wrote — every question an operator added went nowhere, and the questions
    /// customers actually read were compiled into the storefront bundle.
    /// </summary>
    [Fact]
    public async Task Published_questions_are_served_with_their_category()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ContentEntries.AddRange(
                new ContentEntry
                {
                    Slug = "faq-shipping-time",
                    Title = "زمان ارسال چقدر است؟",
                    Kind = ContentKind.Faq,
                    Excerpt = "ارسال",
                    Body = "بین دو تا پنج روز کاری.",
                    Status = ContentStatus.Published,
                },
                new ContentEntry
                {
                    Slug = "faq-draft",
                    Title = "هنوز نوشته نشده",
                    Kind = ContentKind.Faq,
                    Body = "…",
                    Status = ContentStatus.Draft,
                });

            await db.SaveChangesAsync();
        });

        var faqs = (await _public.GetFromJsonAsync<JsonElement>("/api/faqs")).EnumerateArray().ToList();

        var faq = Assert.Single(faqs);
        Assert.Equal("زمان ارسال چقدر است؟", faq.GetProperty("question").GetString());
        Assert.Equal("بین دو تا پنج روز کاری.", faq.GetProperty("answer").GetString());
        Assert.Equal("ارسال", faq.GetProperty("category").GetString());
    }

    /// <summary>
    /// A question with no answer is one somebody started and did not finish.
    /// Serving it would put an empty accordion on the page.
    /// </summary>
    [Fact]
    public async Task A_question_with_no_answer_is_not_served()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ContentEntries.Add(new ContentEntry
            {
                Slug = "faq-unanswered",
                Title = "پرسشی بدون پاسخ",
                Kind = ContentKind.Faq,
                Body = "",
                Status = ContentStatus.Published,
            });

            await db.SaveChangesAsync();
        });

        var faqs = await _public.GetFromJsonAsync<JsonElement>("/api/faqs");

        Assert.Empty(faqs.EnumerateArray());
    }

    /// <summary>
    /// Empty rather than a 404 for a shop with no questions: the storefront
    /// falls back to the set it shipped with, so an empty list is the signal.
    /// </summary>
    [Fact]
    public async Task A_shop_with_no_questions_answers_with_an_empty_list()
    {
        var faqs = await _public.GetFromJsonAsync<JsonElement>("/api/faqs");

        Assert.Empty(faqs.EnumerateArray());
    }

    /// <summary>
    /// Archiving a page takes it off the site. Before the storefront read these
    /// at all there was nothing to take off.
    /// </summary>
    [Fact]
    public async Task An_archived_page_stops_being_served()
    {
        await _factory.WithDbAsync(async db =>
        {
            var page = new ContentEntry
            {
                Slug = "returns",
                Title = "شرایط مرجوعی",
                Kind = ContentKind.Page,
                Body = "متن",
                Status = ContentStatus.Published,
            };

            page.SoftDelete(DateTimeOffset.UtcNow);
            db.ContentEntries.Add(page);

            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound, (await _public.GetAsync("/api/pages/returns")).StatusCode);
    }
}
