using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// A password reset ends the sessions that were open on the old one.
/// </summary>
/// <remarks>
/// Both ways into an account are self-contained by design — a signed cookie and
/// a signed token, each carrying its own expiry, neither needing the database.
/// That is what made them unrevokable: resetting a password is what someone does
/// when they believe another person is in their account, and it used to leave
/// that person exactly where they were until the credential expired on its own.
/// The security stamp is what closes it, and these are the tests that say so.
/// </remarks>
public sealed class SessionRevocationTests : IAsyncLifetime, IDisposable
{
    private const string Email = "revoke@bojan.test";
    private const string Phone = "09121110088";
    private const string OldPassword = "old-password-1";
    private const string NewPassword = "new-password-2";

    private readonly BojanApiFactory _factory = new();
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        using var anonymous = _factory.CreateClient();
        var registered = await anonymous.PostAsJsonAsync(
            "/api/auth/register", new { phone = Phone, email = Email, password = OldPassword });

        registered.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            _customerId = (await db.Customers.SingleAsync(c => c.Phone == Phone)).Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    /// <summary>Drives the real reset flow and returns the token that was emailed.</summary>
    private async Task ResetPasswordAsync()
    {
        using var anonymous = _factory.CreateClient();

        (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email }))
            .EnsureSuccessStatusCode();

        // The link is never stored, only its hash — so the token is taken from
        // the message that was sent, the way the customer would get it.
        var token = _factory.Email.ResetTokenFor(Email)!;

        (await anonymous.PostAsJsonAsync(
                "/api/auth/reset-password", new { token, password = NewPassword }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_session_works_before_the_password_is_reset()
    {
        using var client = _factory.CreateCustomerClient(_customerId);

        var response = await client.GetAsync("/api/me/wallet");
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The point of the whole mechanism: the credential that was valid a moment
    /// ago is not valid now, without waiting for it to expire.
    /// </summary>
    [Fact]
    public async Task Resetting_the_password_stops_a_session_that_was_already_open()
    {
        using var client = _factory.CreateCustomerClient(_customerId);
        (await client.GetAsync("/api/me/wallet")).EnsureSuccessStatusCode();

        await ResetPasswordAsync();

        var afterReset = await client.GetAsync("/api/me/wallet");
        Assert.Equal(HttpStatusCode.Forbidden, afterReset.StatusCode);
    }

    /// <summary>Signing in again produces a session stamped with the new value, which works.</summary>
    [Fact]
    public async Task Signing_in_again_after_a_reset_produces_a_working_session()
    {
        await ResetPasswordAsync();

        Guid rotated = default;
        await _factory.WithDbAsync(async db =>
            rotated = (await db.Customers.SingleAsync(c => c.Id == _customerId)).SecurityStamp);

        using var reissued = _factory.CreateCustomerClient(_customerId, rotated);
        (await reissued.GetAsync("/api/me/wallet")).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A session minted before the stamp existed carries none, and is refused
    /// rather than waved through — a credential that predates the revocation
    /// check is exactly the one revocation could not otherwise reach.
    /// </summary>
    [Fact]
    public async Task A_session_carrying_no_stamp_is_refused()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", BojanApiFactory.TrustedProxyKey);
        client.DefaultRequestHeaders.Add("X-Customer-Id", _customerId.ToString());

        var response = await client.GetAsync("/api/me/wallet");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A stamp that was never issued is no better than none at all.</summary>
    [Fact]
    public async Task A_session_carrying_someone_elses_stamp_is_refused()
    {
        using var client = _factory.CreateCustomerClient(_customerId, Guid.NewGuid());

        var response = await client.GetAsync("/api/me/wallet");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>The reset itself still works — the guard above must not have broken the flow it protects.</summary>
    [Fact]
    public async Task The_new_password_signs_in_and_the_old_one_does_not()
    {
        await ResetPasswordAsync();

        using var anonymous = _factory.CreateClient();

        var withOld = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { identity = Email, password = OldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);

        var withNew = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { identity = Email, password = NewPassword });
        withNew.EnsureSuccessStatusCode();
    }

    /// <summary>The reset link is one-use, and every other outstanding one dies with it.</summary>
    [Fact]
    public async Task A_reset_token_cannot_be_replayed()
    {
        using var anonymous = _factory.CreateClient();

        (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email }))
            .EnsureSuccessStatusCode();

        var token = _factory.Email.ResetTokenFor(Email)!;

        (await anonymous.PostAsJsonAsync(
                "/api/auth/reset-password", new { token, password = NewPassword }))
            .EnsureSuccessStatusCode();

        var replayed = await anonymous.PostAsJsonAsync(
            "/api/auth/reset-password", new { token, password = "third-password-3" });

        Assert.Equal(HttpStatusCode.BadRequest, replayed.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.True(await db.PasswordResetTokens.AllAsync(t => t.ConsumedAtUtc != null)));
    }
}
