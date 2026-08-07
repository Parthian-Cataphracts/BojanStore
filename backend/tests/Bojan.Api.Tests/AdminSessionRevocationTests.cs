using System.Net;
using System.Net.Http.Json;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// An operator's session is revocable too.
/// </summary>
/// <remarks>
/// The panel's cookie is signed, self-contained and lasts a working day. Role
/// and <c>IsActive</c> were already read from the database on every request, so
/// those two changes reached an open session — but a password change did not,
/// which is the wrong one to miss: changing a password is what an operator does
/// when they believe someone else has their session, and it left that person
/// exactly where they were until the cookie expired on its own.
/// </remarks>
public sealed class AdminSessionRevocationTests : IAsyncLifetime, IDisposable
{
    private const string CurrentPassword = "operator-password-1";
    private const string ReplacementPassword = "operator-password-2";

    private readonly BojanApiFactory _factory = new();
    private Guid _ownerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        // Hashed by the shipped hasher rather than by something this test
        // decided a hash looks like — the change endpoint verifies the current
        // password before it will do anything.
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await _factory.WithDbAsync(async db =>
        {
            var owner = await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test");
            owner.PasswordHash = hasher.Hash(CurrentPassword);
            await db.SaveChangesAsync();
            _ownerId = owner.Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> StampAsync()
    {
        Guid stamp = default;
        await _factory.WithDbAsync(async db =>
            stamp = (await db.AdminUsers.SingleAsync(a => a.Id == _ownerId)).SecurityStamp);
        return stamp;
    }

    [Fact]
    public async Task A_stamped_session_reaches_the_panel()
    {
        using var client = _factory.CreateAdminClient(_ownerId);

        (await client.GetAsync("/api/admin/dashboard")).EnsureSuccessStatusCode();
    }

    /// <summary>The point of the mechanism: valid a moment ago, not valid now.</summary>
    [Fact]
    public async Task Changing_the_password_stops_a_session_that_was_already_open()
    {
        using var open = _factory.CreateAdminClient(_ownerId);
        (await open.GetAsync("/api/admin/dashboard")).EnsureSuccessStatusCode();

        using var changing = _factory.CreateAdminClient(_ownerId);
        (await changing.PostAsJsonAsync(
                "/api/admin/me/password",
                new { currentPassword = CurrentPassword, newPassword = ReplacementPassword }))
            .EnsureSuccessStatusCode();

        var afterChange = await open.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, afterChange.StatusCode);
    }

    /// <summary>Signing in again produces a session stamped with the new value, which works.</summary>
    [Fact]
    public async Task Signing_in_again_after_a_change_produces_a_working_session()
    {
        using var changing = _factory.CreateAdminClient(_ownerId);
        (await changing.PostAsJsonAsync(
                "/api/admin/me/password",
                new { currentPassword = CurrentPassword, newPassword = ReplacementPassword }))
            .EnsureSuccessStatusCode();

        using var reissued = _factory.CreateAdminClient(_ownerId, await StampAsync());
        (await reissued.GetAsync("/api/admin/dashboard")).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A cookie minted before the column existed carries no stamp, and is
    /// refused rather than waved through.
    /// </summary>
    [Fact]
    public async Task A_session_carrying_no_stamp_is_refused()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", BojanApiFactory.TrustedProxyKey);
        client.DefaultRequestHeaders.Add("X-Admin-User", _ownerId.ToString());

        var response = await client.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_session_carrying_someone_elses_stamp_is_refused()
    {
        using var client = _factory.CreateAdminClient(_ownerId, Guid.NewGuid());

        var response = await client.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Sign-in has to hand back the stamp, or the panel has nothing to send.</summary>
    [Fact]
    public async Task Sign_in_returns_the_stamp_the_panel_puts_in_its_cookie()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/admin/auth/login", new { identity = "owner@bojan.test", password = CurrentPassword });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        Assert.Equal(await StampAsync(), Guid.Parse(body!.SecurityStamp!));
    }

    private sealed record LoginBody(string Id, string Role, string? SecurityStamp);
}
