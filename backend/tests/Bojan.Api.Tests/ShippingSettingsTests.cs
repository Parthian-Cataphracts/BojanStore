using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The shipping tiers, which the panel could not change at all.
/// </summary>
/// <remarks>
/// The settings screen wrote three prices into the generic settings table,
/// which nothing read, while the figure the shopper was charged came from
/// <c>ShippingMethod</c> rows only the seeder had ever written. A shop whose
/// courier put its prices up had to be redeployed to follow.
/// </remarks>
public sealed class ShippingSettingsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _owner;
    private Guid _support;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _owner = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@shipping.test")).Id;
            _support = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@shipping.test")).Id;

            db.ShippingMethods.AddRange(
                new ShippingMethod { Code = "standard", Title = "استاندارد", Price = new Money(45_000) },
                new ShippingMethod { Code = "express", Title = "سریع", Price = new Money(85_000) });

            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private async Task<HttpResponseMessage> SaveAsync(Guid actor, object body)
    {
        using var client = _factory.CreateAdminClient(actor);
        return await client.PostAsJsonAsync("/api/admin/shipping/methods", body);
    }

    private static object Tier(string code, long price, bool active = true) =>
        new { code, title = "روش ارسال", price, estimate = "۲ تا ۳ روز کاری", isActive = active };

    [Fact]
    public async Task An_owner_changes_a_price_and_the_checkout_charges_it()
    {
        var response = await SaveAsync(_owner, new { methods = new[] { Tier("standard", 59_000), Tier("express", 99_000) } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(
                59_000,
                (await db.ShippingMethods.FirstAsync(m => m.Code == "standard")).Price.Amount));
    }

    /// <summary>
    /// The set of tiers is a change to the checkout screens too — they submit
    /// these codes as constants — so a settings form must not be able to invent
    /// one, and a request that names an unknown code changes nothing.
    /// </summary>
    [Fact]
    public async Task An_unknown_code_creates_nothing()
    {
        await SaveAsync(_owner, new { methods = new[] { Tier("free_forever", 0) } });

        await _factory.WithDbAsync(async db =>
            Assert.Equal(2, await db.ShippingMethods.CountAsync()));
    }

    [Fact]
    public async Task Switching_every_tier_off_is_refused()
    {
        var response = await SaveAsync(
            _owner,
            new { methods = new[] { Tier("standard", 45_000, active: false), Tier("express", 85_000, active: false) } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The version of that rule which the first one missed: a request naming
    /// one tier was judged against its own contents, so switching off the last
    /// one that was still on passed — and the checkout was left with nothing to
    /// pick and no way to place an order.
    /// </summary>
    [Fact]
    public async Task Switching_off_the_last_active_tier_is_refused_even_one_at_a_time()
    {
        var first = await SaveAsync(_owner, new { methods = new[] { Tier("express", 85_000, active: false) } });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await SaveAsync(_owner, new { methods = new[] { Tier("standard", 45_000, active: false) } });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.True(await db.ShippingMethods.AnyAsync(m => m.IsActive)));
    }

    [Fact]
    public async Task A_price_beyond_the_ceiling_is_refused()
    {
        var response = await SaveAsync(_owner, new { methods = new[] { Tier("standard", 999_999_999_999) } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_support_operator_cannot_change_what_the_shop_charges()
    {
        var response = await SaveAsync(_support, new { methods = new[] { Tier("standard", 1) } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(
                45_000,
                (await db.ShippingMethods.FirstAsync(m => m.Code == "standard")).Price.Amount));
    }
}
