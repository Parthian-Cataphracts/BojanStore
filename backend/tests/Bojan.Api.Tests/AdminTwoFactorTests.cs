using System.Net;
using System.Net.Http.Json;
using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bojan.Api.Tests;

/// <summary>
/// A password alone must not open the panel for an account that has a second
/// factor.
/// </summary>
/// <remarks>
/// <c>POST /auth/login</c> used to answer <c>requiresTwoFactor: true</c> and
/// hand over a full admin token in the same body, which made the factor
/// advisory: anything that read the token was already signed in. These cover
/// the split — no token until a code verifies — and the properties of the
/// challenge that stand between the two halves.
/// </remarks>
public sealed class AdminTwoFactorTests : IClassFixture<AdminTwoFactorTests.FixedClockFactory>, IAsyncLifetime
{
    /// <summary>
    /// A TOTP code is only valid for a thirty-second step, so a test that finds
    /// one against the wall clock and then sends it over HTTP is racing that
    /// boundary — rarely, and only when the machine is loaded enough for the
    /// search and the round trip to straddle it. Freezing the clock the API
    /// verifies against removes the race rather than making it less likely.
    /// </summary>
    public sealed class FixedClockFactory : BojanApiFactory
    {
        public sealed class FixedClock : IDateTimeProvider
        {
            public DateTimeOffset UtcNow { get; } = DateTimeOffset.UtcNow;
        }

        public FixedClock Clock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDateTimeProvider>();
                services.AddSingleton<IDateTimeProvider>(Clock);
            });
        }
    }

    private readonly FixedClockFactory _factory;
    private readonly HttpClient _client;

    public AdminTwoFactorTests(FixedClockFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    private const string Password = "the-real-password-123";

    private sealed record LoginBody(
        string? Id,
        string? Name,
        string? Email,
        string? Role,
        bool? RequiresTwoFactor,
        string? Token,
        string? Challenge);

    private async Task<(Guid Id, string Secret)> SeedAdminWithTwoFactorAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var secret = Totp.GenerateSecret();
        var admin = new AdminUser
        {
            Name = "Two Factor Admin",
            Email = email,
            PasswordHash = hasher.Hash(Password),
            Role = AdminRole.Owner,
            TwoFactorEnabled = true,
            TwoFactorSecret = secret,
        };

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();

        return (admin.Id, secret);
    }

    private Task<HttpResponseMessage> LoginAsync(string email) =>
        _client.PostAsJsonAsync("/api/admin/auth/login", new { identity = email, password = Password });

    /// <summary>
    /// Reproduces a valid code the way an authenticator app would, so the test
    /// exercises the real verification rather than a stub of it.
    /// </summary>
    /// <remarks>
    /// Searched against the same frozen instant the API verifies at, so the
    /// code cannot age out of its step between being found here and being
    /// checked there.
    /// </remarks>
    private string CurrentCode(string secret)
    {
        // Totp only exposes verification, so the code is found by asking it
        // about each of the million possibilities the current step admits —
        // which is why the search is bounded and fast in practice: the answer
        // is deterministic for a given secret and instant.
        var now = _factory.Clock.UtcNow;
        for (var candidate = 0; candidate < 1_000_000; candidate++)
        {
            var code = candidate.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            if (Totp.Verify(secret, code, now))
            {
                return code;
            }
        }

        throw new InvalidOperationException("No code verified against the secret — Totp.Verify is broken.");
    }

    [Fact]
    public async Task Password_alone_yields_no_token_for_an_account_with_a_second_factor()
    {
        await SeedAdminWithTwoFactorAsync("2fa-no-token@bojan.example");

        var response = await LoginAsync("2fa-no-token@bojan.example");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();

        Assert.True(body!.RequiresTwoFactor);
        Assert.Null(body.Token);
        Assert.False(string.IsNullOrWhiteSpace(body.Challenge));
    }

    [Fact]
    public async Task An_account_without_a_second_factor_still_signs_in_on_the_password_alone()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            db.AdminUsers.Add(new AdminUser
            {
                Name = "Single Factor Admin",
                Email = "1fa@bojan.example",
                PasswordHash = hasher.Hash(Password),
                Role = AdminRole.Sales,
            });
            await db.SaveChangesAsync();
        }

        var response = await LoginAsync("1fa@bojan.example");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();

        Assert.Null(body!.RequiresTwoFactor);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Null(body.Challenge);
    }

    [Fact]
    public async Task The_challenge_plus_a_valid_code_completes_the_sign_in()
    {
        var (adminId, secret) = await SeedAdminWithTwoFactorAsync("2fa-complete@bojan.example");

        var login = await LoginAsync("2fa-complete@bojan.example");
        var challenge = (await login.Content.ReadFromJsonAsync<LoginBody>())!.Challenge;

        var response = await _client.PostAsJsonAsync(
            "/api/admin/auth/2fa",
            new { challenge, code = CurrentCode(secret) });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();

        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(adminId.ToString(), body.Id);

        // The sign-in the operators list shows is stamped only once the second
        // factor clears, not when the password was accepted.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
        var stored = await db.AdminUsers.AsNoTracking().FirstAsync(a => a.Id == adminId);
        Assert.NotNull(stored.LastLoginAtUtc);
    }

    [Fact]
    public async Task A_wrong_code_is_refused()
    {
        var (_, secret) = await SeedAdminWithTwoFactorAsync("2fa-wrong@bojan.example");

        var login = await LoginAsync("2fa-wrong@bojan.example");
        var challenge = (await login.Content.ReadFromJsonAsync<LoginBody>())!.Challenge;

        // One digit off whatever the real code is, so this never collides with it.
        var valid = CurrentCode(secret);
        var wrong = valid == "000000" ? "000001" : "000000";

        var response = await _client.PostAsJsonAsync("/api/admin/auth/2fa", new { challenge, code = wrong });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_forged_challenge_is_refused()
    {
        var (_, secret) = await SeedAdminWithTwoFactorAsync("2fa-forged@bojan.example");

        var response = await _client.PostAsJsonAsync(
            "/api/admin/auth/2fa",
            new { challenge = "not.a.real.token", code = CurrentCode(secret) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The session token and the challenge are different scopes, and the
    /// exchange must not accept the former.
    /// </summary>
    /// <remarks>
    /// Without the scope check on the way back in, anyone already holding an
    /// admin token could present it here for a *different* account's second
    /// factor step — the endpoint would read its subject and sign that subject
    /// in. It reads only <c>admin-2fa</c>.
    /// </remarks>
    [Fact]
    public async Task A_full_admin_token_is_not_accepted_as_a_challenge()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            db.AdminUsers.Add(new AdminUser
            {
                Name = "Token Holder",
                Email = "token-holder@bojan.example",
                PasswordHash = hasher.Hash(Password),
                Role = AdminRole.Support,
            });
            await db.SaveChangesAsync();
        }

        var login = await LoginAsync("token-holder@bojan.example");
        var sessionToken = (await login.Content.ReadFromJsonAsync<LoginBody>())!.Token;

        var (_, secret) = await SeedAdminWithTwoFactorAsync("2fa-scope@bojan.example");

        var response = await _client.PostAsJsonAsync(
            "/api/admin/auth/2fa",
            new { challenge = sessionToken, code = CurrentCode(secret) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A flag with no secret behind it would otherwise lock the account out:
    /// nothing could produce a code for it.
    /// </summary>
    [Fact]
    public async Task An_enabled_flag_with_no_secret_does_not_block_sign_in()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            db.AdminUsers.Add(new AdminUser
            {
                Name = "Half Enrolled",
                Email = "half-enrolled@bojan.example",
                PasswordHash = hasher.Hash(Password),
                Role = AdminRole.Product,
                TwoFactorEnabled = true,
                TwoFactorSecret = null,
            });
            await db.SaveChangesAsync();
        }

        var response = await LoginAsync("half-enrolled@bojan.example");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }
}
