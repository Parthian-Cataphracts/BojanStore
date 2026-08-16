using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Customers;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Persistence.Seed;

/// <summary>
/// Loads the design's own catalogue into an empty database.
/// </summary>
/// <remarks>
/// <para>
/// The content in <c>catalogue.json</c> is the frontend's fixture set, lifted
/// mechanically from <c>apps/storefront/src/lib/mock/</c> rather than retyped —
/// the README describes those fixtures as "lifted verbatim from the Stitch
/// design screens", and re-typing them into C# would guarantee they drifted.
/// Seeding from them is what makes <c>NEXT_PUBLIC_USE_MOCK_DATA=false</c>
/// render the same screens as <c>true</c> does, which is
/// <c>BACKEND.md</c>'s definition of done for Phase 2.
/// </para>
/// <para>
/// Every step is skipped when its table already has rows. The seeder is safe to
/// run on every start in development and does nothing on the second run; it is
/// not a migration and does not belong in production, which is why
/// <c>Program.cs</c> gates it on configuration rather than calling it
/// unconditionally.
/// </para>
/// </remarks>
public sealed class CatalogueSeeder(BojanDbContext db, IPasswordHasher passwordHasher, ILogger<CatalogueSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SeedAsync(
        string? adminPassword,
        string? adminPhone,
        string? developmentCustomerPhone = null,
        CancellationToken cancellationToken = default)
    {
        var data = Load();

        await SeedCheckoutMethodsAsync(data, cancellationToken);
        await SeedCategoriesAsync(data, cancellationToken);
        await SeedBrandsAsync(data, cancellationToken);
        await SeedProductsAsync(data, cancellationToken);
        await SeedCollectionsAsync(data, cancellationToken);
        await SeedArticlesAsync(data, cancellationToken);
        await SeedGiftBundlesAsync(data, cancellationToken);
        await SeedCouponAsync(cancellationToken);
        await SeedAdminAsync(adminPassword, adminPhone, cancellationToken);
        await SeedDevelopmentCustomerAsync(developmentCustomerPhone, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Catalogue seed complete.");
    }

    /// <summary>
    /// The account the development sign-in code signs into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The profile and both addresses are the design's own
    /// (<c>lib/mock/catalog.ts</c>'s <c>mockUser</c> and <c>mockAddresses</c>),
    /// so the account screens show what the mockups show rather than an empty
    /// state a developer then has to fill in by hand before they can look at
    /// anything.
    /// </para>
    /// <para>
    /// <paramref name="phone"/> is only ever non-null in Development —
    /// <c>Program.cs</c> passes it from <c>Auth:DevOtp:Phone</c> and only when
    /// the host says so. Without it this does nothing.
    /// </para>
    /// </remarks>
    private async Task SeedDevelopmentCustomerAsync(string? phone, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone) || await db.Customers.AnyAsync(c => c.Phone == phone, cancellationToken))
        {
            return;
        }

        var customer = new Customer
        {
            Phone = phone,
            FirstName = "نیلوفر",
            LastName = "احمدی",
            Email = "niloofar@example.com",
            BirthDate = new DateOnly(1994, 5, 18),
            City = "تهران",
        };

        customer.CreditWallet(new Money(850_000));
        customer.AddLoyaltyPoints(1_240);

        customer.AddAddress(new Address
        {
            CustomerId = customer.Id,
            Title = "خانه",
            Recipient = "نیلوفر احمدی",
            Phone = phone,
            Province = "تهران",
            City = "تهران",
            PostalCode = "1968843561",
            Line = "خیابان ولیعصر، بالاتر از پارک ساعی، کوچه شهید نیکنام، پلاک ۱۲، واحد ۴",
            IsDefault = true,
        });

        customer.AddAddress(new Address
        {
            CustomerId = customer.Id,
            Title = "محل کار",
            Recipient = "نیلوفر احمدی",
            Phone = "02188776655",
            Province = "تهران",
            City = "تهران",
            PostalCode = "1587634512",
            Line = "خیابان سهروردی شمالی، ساختمان نگین، طبقه ۵، واحد ۱۸",
            IsDefault = false,
        });

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Development sign-in: seeded the demo customer {Phone}. This account exists only because Auth:DevOtp is configured.",
            phone);
    }

    private static SeedData Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resource = "Bojan.Infrastructure.Persistence.Seed.catalogue.json";

        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resource}' is missing. It is generated from the frontend fixtures — see the seeder's remarks.");

        return JsonSerializer.Deserialize<SeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"'{resource}' did not deserialise into seed data.");
    }

    private async Task SeedCheckoutMethodsAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (!await db.ShippingMethods.AnyAsync(cancellationToken))
        {
            var order = 0;
            foreach (var method in data.ShippingMethods)
            {
                db.ShippingMethods.Add(new ShippingMethod
                {
                    // The id from the fixture is the wire id the checkout
                    // submits — see ShippingMethod.Code.
                    Code = method.Id,
                    Title = method.Label,
                    Price = new Money(method.Price),
                    Estimate = method.Note,
                    Icon = method.Icon,
                    SortOrder = order++,
                });
            }
        }

        if (!await db.PaymentMethods.AnyAsync(cancellationToken))
        {
            var order = 0;
            foreach (var method in data.PaymentMethods)
            {
                db.PaymentMethods.Add(new PaymentMethod
                {
                    Code = method.Id,
                    Title = method.Label,
                    Note = method.Note,
                    RequiresGateway = method.Id == "gateway",
                    UsesWallet = method.Id == "wallet",
                    Icon = method.Icon,
                    SortOrder = order++,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedCategoriesAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var category in data.Categories)
        {
            var parent = new Category
            {
                Slug = category.Slug,
                Name = category.Name,
                Icon = category.Icon,
            };

            db.Categories.Add(parent);

            foreach (var child in category.Children ?? [])
            {
                db.Categories.Add(new Category
                {
                    Slug = child.Slug,
                    Name = child.Name,
                    Icon = child.Icon,
                    ParentId = parent.Id,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedBrandsAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (await db.Brands.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var brand in data.Brands)
        {
            db.Brands.Add(new Brand
            {
                Slug = brand.Slug,
                Name = brand.Name,
                Tagline = brand.Tagline,
                Description = brand.Description,
                CoverUrl = brand.Cover,
                LogoUrl = brand.Logo,
                IsFeatured = brand.Featured ?? false,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Products, plus the brands and categories they reference that the two
    /// lists above did not already create.
    /// </summary>
    /// <remarks>
    /// The fixtures name a brand on every product but only profile six of them,
    /// so a product's <c>brandSlug</c> is the authority and any brand missing
    /// from the directory is created from it. Without that, a third of the
    /// catalogue would have no brand to join to and would vanish from every
    /// listing.
    /// </remarks>
    private async Task SeedProductsAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var brands = await db.Brands.ToDictionaryAsync(b => b.Slug, cancellationToken);
        var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, cancellationToken);

        foreach (var product in data.Products)
        {
            if (!brands.TryGetValue(product.BrandSlug, out var brand))
            {
                brand = new Brand { Slug = product.BrandSlug, Name = product.Brand };
                db.Brands.Add(brand);
                brands[product.BrandSlug] = brand;
            }

            if (!categories.TryGetValue(product.CategorySlug, out var category))
            {
                category = new Category { Slug = product.CategorySlug, Name = product.CategoryName };
                db.Categories.Add(category);
                categories[product.CategorySlug] = category;
            }

            var entity = new Product
            {
                Slug = product.Slug,
                Title = product.Title,
                BrandId = brand.Id,
                CategoryId = category.Id,
                Price = new Money(product.Price),
                CompareAtPrice = product.CompareAtPrice is { } compareAt ? new Money(compareAt) : null,
                // The fixtures carry no cost price — it is a panel-only field
                // with no design screen to lift a value from. Left at zero
                // rather than invented, so a margin report reads as "not
                // recorded" instead of as a fabricated profit.
                CostPrice = Money.Zero,
                // The design draws several products out of stock; the fixture
                // records that faithfully, so it is seeded as-is rather than
                // topped up to make the shop look busier.
                Stock = product.Stock,
                ImageUrl = product.Image,
                ImageAlt = product.ImageAlt,
                Description = product.Description,
                IsNew = product.IsNew,
                IsBestseller = product.IsBestseller,
                Sku = SkuFor(product.Slug),
            };

            foreach (var (url, index) in (product.Gallery ?? []).Select((url, index) => (url, index)))
            {
                entity.AddGalleryImage(url, index);
            }

            foreach (var spec in product.Specs ?? [])
            {
                entity.AddSpec(spec.Label, spec.Value);
            }

            db.Products.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
        await SeedVariantAxesAsync(data, cancellationToken);
    }

    /// <summary>
    /// The design draws variant axes on one product page, so they are attached
    /// to the first product rather than repeated across the catalogue.
    /// </summary>
    private async Task SeedVariantAxesAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (data.VariantAxes.Count == 0 || await db.ProductVariantAxes.AnyAsync(cancellationToken))
        {
            return;
        }

        var first = await db.Products.OrderBy(p => p.Slug).FirstOrDefaultAsync(cancellationToken);
        if (first is null)
        {
            return;
        }

        var axisOrder = 0;
        foreach (var axis in data.VariantAxes)
        {
            var entity = new ProductVariantAxis
            {
                ProductId = first.Id,
                Key = axis.Id,
                Label = axis.Label,
                Kind = axis.Kind == "swatch" ? VariantAxisKind.Swatch : VariantAxisKind.Chip,
                SortOrder = axisOrder++,
            };

            var optionOrder = 0;
            foreach (var option in axis.Options)
            {
                entity.AddOption(option.Id, option.Label, option.Hex, option.Available, optionOrder++);
            }

            db.ProductVariantAxes.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedCollectionsAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (await db.Collections.AnyAsync(cancellationToken))
        {
            return;
        }

        var products = await db.Products.ToDictionaryAsync(p => p.Slug, p => p.Id, cancellationToken);

        foreach (var collection in data.Collections)
        {
            var entity = new Collection
            {
                Slug = collection.Slug,
                Title = collection.Title,
                Summary = collection.Summary,
                CoverUrl = collection.Cover,
                EditorialNote = collection.EditorialNote,
                IsFeatured = collection.Featured ?? false,
            };

            var order = 0;
            foreach (var slug in collection.ProductSlugs)
            {
                if (products.TryGetValue(slug, out var productId))
                {
                    entity.AddProduct(productId, order++);
                }
            }

            db.Collections.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedArticlesAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (await db.Articles.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var article in data.Articles)
        {
            var entity = new Article
            {
                Slug = article.Slug,
                Title = article.Title,
                Excerpt = article.Excerpt,
                Category = article.Category,
                CoverUrl = article.Cover,
                PublishedAtUtc = article.PublishedAt,
                ReadingMinutes = article.ReadingMinutes,
                IsFeatured = article.Featured ?? false,
                RecommendedProductSlug = article.RecommendedProductSlug,
            };

            var order = 0;
            foreach (var block in article.Body ?? [])
            {
                var kind = block.Type switch
                {
                    "heading" => ArticleBlockKind.Heading,
                    "product" => ArticleBlockKind.Product,
                    _ => ArticleBlockKind.Paragraph,
                };

                entity.AddBlock(kind, kind == ArticleBlockKind.Product ? null : block.Text, order++);
            }

            db.Articles.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedGiftBundlesAsync(SeedData data, CancellationToken cancellationToken)
    {
        if (await db.GiftBundles.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var bundle in data.GiftBundles)
        {
            db.GiftBundles.Add(new GiftBundle
            {
                Slug = bundle.Slug,
                Title = bundle.Title,
                Summary = bundle.Summary,
                CoverUrl = bundle.Cover,
                Category = bundle.Category,
                PricePerUnit = new Money(bundle.PricePerUnit),
                MinimumQuantity = bundle.MinimumQuantity,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The one discount code the design shows, worth what the design's own
    /// checkout summary subtracts.
    /// </summary>
    private async Task SeedCouponAsync(CancellationToken cancellationToken)
    {
        if (await db.Coupons.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Coupons.Add(new Coupon
        {
            Code = "BOJAN10",
            AmountOff = new Money(120_000),
            MinimumSpend = new Money(500_000),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddYears(1),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The first operator.
    /// </summary>
    /// <remarks>
    /// No default password — the same rule the frontend applies to
    /// <c>ADMIN_DEV_PASSWORD</c>: "there is no default password that works".
    /// Without one configured, the panel simply has no account to sign in with,
    /// which is the correct state for a system nobody has set up yet.
    /// </remarks>
    private async Task SeedAdminAsync(
        string? adminPassword,
        string? adminPhone,
        CancellationToken cancellationToken)
    {
        if (await db.AdminUsers.AnyAsync(cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "No Seed:AdminPassword configured, so no operator account was created. The admin panel has nothing to sign in with until one is set.");
            return;
        }

        /*
          A phone number, because a shopper here *is* a phone number.

          The owner used to be seeded as an operator row with an e-mail and a
          password and nothing else, and that is precisely why the person who
          owns the shop could not sign in to it: the panel took their
          credential and the storefront had no account to match it to. The
          owner is a customer who has been granted the panel, like every other
          operator, so the account comes first.
        */
        var phone = string.IsNullOrWhiteSpace(adminPhone) ? DefaultOwnerPhone : adminPhone.Trim();

        var existing = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);
        var account = existing ?? new Customer
        {
            Phone = phone,
            Email = OwnerEmail,
            FirstName = "مدیر",
            LastName = "سیستم",
        };

        // Set on an account this call created, and on one it found that has
        // never had a password — the second is the installer being run against
        // a number that had already shopped here. An existing password is left
        // alone: it is that person's, and the installer does not get to replace
        // it behind their back.
        account.PasswordHash ??= passwordHasher.Hash(adminPassword);

        if (existing is null)
        {
            db.Customers.Add(account);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.AdminUsers.Add(new AdminUser
        {
            CustomerId = account.Id,
            Name = "مدیر سیستم",
            Email = account.Email ?? OwnerEmail,
            Phone = account.Phone,
            Role = AdminRole.Owner,
        });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Owner seeded. Sign in on both the panel and the storefront with {Phone}.", phone);
    }

    /// <summary>
    /// Where the owner's account lands when the installer did not ask for a
    /// number.
    /// </summary>
    /// <remarks>
    /// A real, well-formed Iranian mobile number rather than a placeholder,
    /// because it has to be one the sign-in form will accept — the owner has to
    /// be able to type it. Deployments are expected to set
    /// <c>Seed:AdminPhone</c>; this is what keeps a bare <c>docker compose up</c>
    /// from producing an owner nobody can sign in as, which is the fault this
    /// whole arrangement exists to fix.
    /// </remarks>
    private const string DefaultOwnerPhone = "09000000000";

    private const string OwnerEmail = "admin@bojan.com";

    /// <summary>
    /// A stable stock code derived from the slug, since the fixtures have none.
    /// </summary>
    /// <remarks>
    /// Deterministic on purpose: re-seeding a wiped database produces the same
    /// codes, so a panel screenshot or a saved filter from before the wipe
    /// still means what it did.
    /// </remarks>
    private static string SkuFor(string slug) =>
        $"BZ-{slug.ToUpperInvariant().Replace('-', '_')}";

    // The seed file's own shapes. Deliberately separate from the DTOs: this
    // mirrors the frontend fixtures, which are close to but not identical to
    // the API contracts (a fixture product carries no id the database will use,
    // and a fixture category nests its children inline).
    private sealed record SeedData(
        IReadOnlyList<SeedCategory> Categories,
        IReadOnlyList<SeedBrand> Brands,
        IReadOnlyList<SeedProduct> Products,
        IReadOnlyList<SeedVariantAxis> VariantAxes,
        IReadOnlyList<SeedCollection> Collections,
        IReadOnlyList<SeedArticle> Articles,
        IReadOnlyList<SeedShippingMethod> ShippingMethods,
        IReadOnlyList<SeedPaymentMethod> PaymentMethods,
        IReadOnlyList<SeedGiftBundle> GiftBundles);

    private sealed record SeedCategory(string Slug, string Name, string Icon, IReadOnlyList<SeedCategory>? Children);

    private sealed record SeedBrand(
        string Slug, string Name, string? Tagline, string? Description, string? Logo, string? Cover, bool? Featured);

    private sealed record SeedProduct(
        string Slug,
        string Title,
        string Brand,
        string BrandSlug,
        string CategorySlug,
        string CategoryName,
        long Price,
        long? CompareAtPrice,
        int Stock,
        string Image,
        string ImageAlt,
        IReadOnlyList<string>? Gallery,
        string? Description,
        IReadOnlyList<SeedSpec>? Specs,
        bool IsNew,
        bool IsBestseller);

    private sealed record SeedSpec(string Label, string Value);

    private sealed record SeedVariantAxis(string Id, string Label, string Kind, IReadOnlyList<SeedVariantOption> Options);

    private sealed record SeedVariantOption(string Id, string Label, string? Hex, bool Available);

    private sealed record SeedCollection(
        string Slug,
        string Title,
        string Summary,
        string Cover,
        IReadOnlyList<string> ProductSlugs,
        string? EditorialNote,
        bool? Featured);

    private sealed record SeedArticle(
        string Slug,
        string Title,
        string Excerpt,
        string Category,
        string Cover,
        DateTimeOffset PublishedAt,
        int ReadingMinutes,
        bool? Featured,
        IReadOnlyList<SeedArticleBlock>? Body,
        string? RecommendedProductSlug);

    private sealed record SeedArticleBlock(string Type, string? Text);

    private sealed record SeedShippingMethod(string Id, string Label, string Note, long Price, string Icon);

    private sealed record SeedPaymentMethod(string Id, string Label, string Note, string Icon);

    private sealed record SeedGiftBundle(
        string Slug, string Title, string Summary, string Cover, string Category, long PricePerUnit, int MinimumQuantity);
}
