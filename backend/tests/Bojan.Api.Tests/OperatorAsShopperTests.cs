using System.Net;
using System.Net.Http.Json;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Bojan.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// One person, one account, one password — and two doors.
/// </summary>
/// <remarks>
/// <para>
/// This file used to test a bridge: an operator held their own account with its
/// own password, and signing in to the shop with panel credentials fell through
/// to the operator table and minted a shopping account on the spot. That
/// arrangement is what produced an owner who could not buy from their own shop —
/// the installer created an operator with no phone number, and a shopper here
/// <i>is</i> a phone number.
/// </para>
/// <para>
/// There is no bridge now because there is nothing to bridge. An operator is a
/// shop account that has been granted the panel, so the storefront sign-in that
/// used to need a fallback is simply the sign-in. What is worth asserting is
/// what that buys: the same credential opens both doors, and the panel door
/// additionally asks whether a grant exists.
/// </para>
/// </remarks>
public sealed class OperatorAsShopperTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;

    private const string Password = "operatorPassword123";

    public Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    /// <summary>A shop account, and optionally the panel grant that sits on it.</summary>
    private async Task<(Guid CustomerId, Guid? AdminId)> AddAsync(
        string email,
        string phone,
        AdminRole? role = null,
        bool active = true,
        bool blocked = false)
    {
        var customerId = Guid.Empty;
        Guid? adminId = null;

        await _factory.WithDbAsync(async db =>
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var account = new Customer
            {
                Phone = phone,
                Email = email,
                FirstName = "نگار",
                LastName = "مرادی",
                PasswordHash = hasher.Hash(Password),
                IsBlocked = blocked,
            };

            db.Customers.Add(account);
            await db.SaveChangesAsync();
            customerId = account.Id;

            if (role is { } granted)
            {
                var admin = new AdminUser
                {
                    CustomerId = account.Id,
                    Name = "نگار مرادی",
                    Email = email,
                    Phone = phone,
                    Role = granted,
                    IsActive = active,
                };

                db.AdminUsers.Add(admin);
                await db.SaveChangesAsync();
                adminId = admin.Id;
            }
        });

        return (customerId, adminId);
    }

    private Task<HttpResponseMessage> SignInToShop(string identity, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { identity, password });

    private Task<HttpResponseMessage> SignInToPanel(string identity, string password) =>
        _client.PostAsJsonAsync("/api/admin/auth/login", new { identity, password });

    private sealed record LoginBody(string? Token, string? Phone);

    private sealed record PanelBody(string? Token, string? Role);

    [Fact]
    public async Task The_same_credential_opens_both_doors()
    {
        await AddAsync("both-doors@bojan.test", "09121230001", AdminRole.Support);

        var shop = await SignInToShop("both-doors@bojan.test", Password);
        shop.EnsureSuccessStatusCode();
        Assert.False(string.IsNullOrWhiteSpace((await shop.Content.ReadFromJsonAsync<LoginBody>())!.Token));

        var panel = await SignInToPanel("both-doors@bojan.test", Password);
        panel.EnsureSuccessStatusCode();
        Assert.Equal("support", (await panel.Content.ReadFromJsonAsync<PanelBody>())!.Role);
    }

    /// <summary>The identity is the account's, so either half of it gets in.</summary>
    [Fact]
    public async Task The_panel_takes_the_phone_number_as_readily_as_the_address()
    {
        await AddAsync("by-phone@bojan.test", "09121230002", AdminRole.Owner);

        var panel = await SignInToPanel("09121230002", Password);

        panel.EnsureSuccessStatusCode();
        Assert.Equal("owner", (await panel.Content.ReadFromJsonAsync<PanelBody>())!.Role);
    }

    /// <summary>
    /// The whole point of the grant: a shopper is not an operator until somebody
    /// says so, and the shop door does not care either way.
    /// </summary>
    [Fact]
    public async Task A_shopper_without_a_grant_is_refused_by_the_panel_and_admitted_by_the_shop()
    {
        await AddAsync("just-a-shopper@bojan.test", "09121230003");

        (await SignInToShop("just-a-shopper@bojan.test", Password)).EnsureSuccessStatusCode();

        var panel = await SignInToPanel("just-a-shopper@bojan.test", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, panel.StatusCode);
    }

    /// <summary>
    /// Suspending a grant closes the panel and leaves the shop open — the person
    /// is no longer an operator, not banned from buying socks.
    /// </summary>
    [Fact]
    public async Task A_suspended_grant_closes_the_panel_only()
    {
        await AddAsync("suspended@bojan.test", "09121230004", AdminRole.Support, active: false);

        var panel = await SignInToPanel("suspended@bojan.test", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, panel.StatusCode);

        (await SignInToShop("suspended@bojan.test", Password)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Blocking the account closes both, and that asymmetry with the test above
    /// is the point: the panel is the more dangerous door, so it can never be
    /// the more forgiving one.
    /// </summary>
    [Fact]
    public async Task A_blocked_account_closes_both_doors()
    {
        await AddAsync("blocked@bojan.test", "09121230005", AdminRole.Support, blocked: true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInToPanel("blocked@bojan.test", Password)).StatusCode);

        var shop = await SignInToShop("blocked@bojan.test", Password);
        Assert.NotEqual(HttpStatusCode.OK, shop.StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_is_refused_at_both_doors()
    {
        await AddAsync("wrong-password@bojan.test", "09121230006", AdminRole.Support);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SignInToPanel("wrong-password@bojan.test", "notTheirPassword")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SignInToShop("wrong-password@bojan.test", "notTheirPassword")).StatusCode);
    }

    /// <summary>
    /// One grant per account, held by the unique index rather than by whoever
    /// remembered to check — two operators sharing a customer would be two
    /// people placing orders as one.
    /// </summary>
    [Fact]
    public async Task An_account_cannot_hold_two_grants()
    {
        var (customerId, _) = await AddAsync("one-grant@bojan.test", "09121230007", AdminRole.Support);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _factory.WithDbAsync(async db =>
        {
            db.AdminUsers.Add(new AdminUser
            {
                CustomerId = customerId,
                Name = "دومی",
                Email = "second-grant@bojan.test",
                Phone = "09121230008",
                Role = AdminRole.Sales,
            });

            await db.SaveChangesAsync();
        }));
    }
}
