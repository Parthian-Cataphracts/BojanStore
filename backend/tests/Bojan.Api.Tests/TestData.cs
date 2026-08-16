using Microsoft.EntityFrameworkCore;
using Bojan.Domain.Admin;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;
using Bojan.Infrastructure.Persistence;

namespace Bojan.Api.Tests;

/// <summary>
/// The smallest catalogue an endpoint test can be written against.
/// </summary>
/// <remarks>
/// Deliberately not the seeder: these tests assert on exact prices, stock
/// counts and ownership, and 33 design products would make every arrangement
/// read as "find the one that happens to have stock". Each test builds what it
/// needs and nothing else.
/// </remarks>
public static class TestData
{
    public static async Task<(Guid BrandId, Guid CategoryId)> AddCatalogueAsync(BojanDbContext db)
    {
        var brand = new Brand { Slug = "test-brand", Name = "برند آزمایشی" };
        var category = new Category { Slug = "test-category", Name = "دسته آزمایشی", Icon = "edit" };

        db.Brands.Add(brand);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        return (brand.Id, category.Id);
    }

    public static async Task<Product> AddProductAsync(
        BojanDbContext db,
        Guid brandId,
        Guid categoryId,
        string slug,
        long price,
        int stock,
        bool published = true)
    {
        var product = new Product
        {
            Slug = slug,
            Title = $"محصول {slug}",
            BrandId = brandId,
            CategoryId = categoryId,
            Price = new Money(price),
            Stock = stock,
            ImageUrl = $"https://example.test/{slug}.jpg",
            IsPublished = published,
            Sku = $"BZ-{slug.ToUpperInvariant()}",
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public static async Task<ProductSku> AddSkuAsync(
        BojanDbContext db,
        Guid productId,
        string code,
        long price,
        int stock,
        bool active = true)
    {
        var sku = new ProductSku
        {
            ProductId = productId,
            Code = code,
            Combination = code,
            Price = new Money(price),
            IsActive = active,
        };
        sku.SetStock(stock);

        db.ProductSkus.Add(sku);
        await db.SaveChangesAsync();
        return sku;
    }

    public static async Task<Customer> AddCustomerAsync(BojanDbContext db, string phone)
    {
        var customer = new Customer { Phone = phone, FirstName = "آزمون", LastName = "کاربر" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    public static async Task<Address> AddAddressAsync(BojanDbContext db, Guid customerId)
    {
        var address = new Address
        {
            CustomerId = customerId,
            Title = "خانه",
            Recipient = "آزمون کاربر",
            Phone = "09121234567",
            Province = "تهران",
            City = "تهران",
            PostalCode = "1234567890",
            Line = "خیابان آزمایشی، پلاک ۱",
            IsDefault = true,
        };

        db.Addresses.Add(address);
        await db.SaveChangesAsync();
        return address;
    }

    /// <summary>
    /// The checkout methods, with the wire codes the frontend actually submits
    /// — see <see cref="ShippingMethod.Code"/>.
    /// </summary>
    public static async Task AddCheckoutMethodsAsync(BojanDbContext db)
    {
        db.ShippingMethods.Add(new ShippingMethod
        {
            Code = "standard",
            Title = "ارسال استاندارد",
            Price = new Money(45_000),
        });

        db.PaymentMethods.Add(new PaymentMethod { Code = "cod", Title = "پرداخت در محل" });
        db.PaymentMethods.Add(new PaymentMethod
        {
            Code = "gateway",
            Title = "پرداخت اینترنتی",
            RequiresGateway = true,
        });

        await db.SaveChangesAsync();
    }

    public static async Task<Coupon> AddCouponAsync(
        BojanDbContext db,
        string code,
        long? amountOff = null,
        int? percentOff = null,
        long? minimumSpend = null)
    {
        var coupon = new Coupon
        {
            Code = code,
            AmountOff = amountOff is { } amount ? new Money(amount) : null,
            PercentOff = percentOff,
            MinimumSpend = minimumSpend is { } minimum ? new Money(minimum) : null,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return coupon;
    }

    /// <summary>
    /// An operator, and the shop account the grant sits on.
    /// </summary>
    /// <remarks>
    /// Both, because an operator is no longer an account of its own: the grant
    /// requires a customer, and that customer is where any password would live.
    /// Most of these tests authenticate through the trusted-proxy scheme, which
    /// asserts the id and reads the role from the grant, so no password is set
    /// here — a test that signs in with one sets it on the customer itself.
    ///
    /// The phone is derived from the e-mail so two operators in one test never
    /// collide on the unique index, and it stays inside the eleven digits the
    /// column allows.
    /// </remarks>
    public static async Task<AdminUser> AddAdminAsync(
        BojanDbContext db,
        AdminRole role,
        string email,
        string? phone = null,
        string? passwordHash = null)
    {
        var number = phone ?? PhoneFor(email);

        var account = await db.Customers.FirstOrDefaultAsync(c => c.Phone == number);
        if (account is null)
        {
            account = new Customer
            {
                Phone = number,
                Email = email,
                FirstName = "اپراتور",
                LastName = role.ToString(),
                PasswordHash = passwordHash,
            };
            db.Customers.Add(account);
            await db.SaveChangesAsync();
        }
        else if (passwordHash is not null)
        {
            account.PasswordHash = passwordHash;
            await db.SaveChangesAsync();
        }

        var admin = new AdminUser
        {
            CustomerId = account.Id,
            Name = $"اپراتور {role}",
            Email = email,
            Phone = number,
            Role = role,
        };

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    /// <summary>A stable, unique 11-digit number for a test address.</summary>
    public static string PhoneFor(string email)
    {
        var digits = Math.Abs(email.GetHashCode(StringComparison.Ordinal)) % 100_000_000;
        return $"0912{digits:D7}"[..11];
    }
}
