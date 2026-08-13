using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Suspending a customer, and setting a password for one.
/// </summary>
/// <remarks>
/// <para>
/// <c>IsBlocked</c> existed for as long as the panel had a status column, and
/// nothing wrote it and nothing enforced it. It was read in four places — the
/// list filter, the status badge, the customer statistics and the notification
/// fan-out — so the panel could show a state the shop had no way to put anybody
/// into, and a row edited straight in the database signed in exactly as before.
/// </para>
/// <para>
/// Most of these are about the enforcement rather than the toggle, because the
/// toggle is a boolean and the enforcement is the security of it: there are four
/// ways into an account here, and a suspension that closes three of them has
/// closed none.
/// </para>
/// </remarks>
public sealed class CustomerSuspensionTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _owner = null!;
    private Guid _customerId;

    private const string Phone = "09121119001";
    private const string Email = "suspended@bojan.test";
    private const string Password = "aVeryLongEnoughPassword1";

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var customer = await TestData.AddCustomerAsync(db, Phone);
            customer.Email = Email;
            await db.SaveChangesAsync();
            _customerId = customer.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "suspension-owner@bojan.test")).Id;
        });

        _owner = _factory.CreateAdminClient(ownerId);

        // Through the API rather than by writing a hash, so the password these
        // tests sign in with is one the shop itself produced.
        var set = await _owner.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = _customerId.ToString(), password = Password });
        set.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> Block(bool blocked) =>
        _owner.PostAsJsonAsync(
            "/api/admin/customers/block",
            new { customerId = _customerId.ToString(), blocked });

    private async Task<Guid> StampAsync()
    {
        Guid stamp = default;
        await _factory.WithDbAsync(async db =>
            stamp = (await db.Customers.AsNoTracking().SingleAsync(c => c.Id == _customerId)).SecurityStamp);
        return stamp;
    }

    private async Task<bool> IsBlockedAsync()
    {
        var blocked = false;
        await _factory.WithDbAsync(async db =>
            blocked = (await db.Customers.AsNoTracking().SingleAsync(c => c.Id == _customerId)).IsBlocked);
        return blocked;
    }

    // --- the toggle ---------------------------------------------------------

    [Fact]
    public async Task Suspending_sets_the_flag_and_ends_open_sessions()
    {
        var before = await StampAsync();

        (await Block(true)).EnsureSuccessStatusCode();

        Assert.True(await IsBlockedAsync());
        // The stamp is what makes the cookie already in a browser stop working.
        // Without this the flag stops the next sign-in and does nothing about
        // the session that is open right now — the one that matters.
        Assert.NotEqual(before, await StampAsync());
    }

    [Fact]
    public async Task Reinstating_clears_the_flag_and_leaves_the_stamp_alone()
    {
        (await Block(true)).EnsureSuccessStatusCode();
        var suspended = await StampAsync();

        (await Block(false)).EnsureSuccessStatusCode();

        Assert.False(await IsBlockedAsync());
        // Nothing to invalidate: their sessions died when they were suspended.
        Assert.Equal(suspended, await StampAsync());
    }

    [Fact]
    public async Task Suspending_twice_is_not_an_error_and_does_not_rotate_again()
    {
        (await Block(true)).EnsureSuccessStatusCode();
        var after = await StampAsync();

        // Two operators on one screen, or one clicking twice.
        (await Block(true)).EnsureSuccessStatusCode();

        Assert.Equal(after, await StampAsync());
    }

    // --- the four ways in ---------------------------------------------------

    [Fact]
    public async Task A_suspended_customer_cannot_sign_in_with_a_code()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/otp/request", new { phone = Phone });
        var code = _factory.Sms.LastCodeFor(Phone);

        (await Block(true)).EnsureSuccessStatusCode();

        var verify = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone = Phone, code });

        // Forbidden rather than a wrong-code answer: the code was right and the
        // answer is still no, and telling them it was wrong sends them round
        // the loop instead of to support.
        Assert.Equal(HttpStatusCode.Forbidden, verify.StatusCode);
    }

    [Fact]
    public async Task A_suspended_customer_cannot_sign_in_with_their_password()
    {
        (await Block(true)).EnsureSuccessStatusCode();

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { identity = Phone, password = Password });

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task A_suspended_customer_is_sent_no_reset_link()
    {
        (await Block(true)).EnsureSuccessStatusCode();

        var client = _factory.CreateClient();
        var asked = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });

        // Answers exactly as it does for an address nobody has — the silence is
        // what stops this being a membership oracle, so the refusal must not
        // sound different from the ordinary case.
        asked.EnsureSuccessStatusCode();

        var tokens = 0;
        await _factory.WithDbAsync(async db =>
            tokens = await db.PasswordResetTokens.CountAsync(t => t.CustomerId == _customerId));

        Assert.Equal(0, tokens);
    }

    [Fact]
    public async Task A_reset_link_issued_before_the_suspension_stops_working()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });

        // The raw token is never stored, so it is read from the mail the shop
        // sent — the same string the customer would click.
        var token = _factory.Email.ResetTokenFor(Email);
        Assert.NotNull(token);

        (await Block(true)).EnsureSuccessStatusCode();

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { token, password = "anotherLongEnoughPassword1" });

        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
    }

    // --- setting a password -------------------------------------------------

    [Fact]
    public async Task The_password_an_operator_sets_is_the_one_that_signs_in()
    {
        const string chosen = "counterIssuedPassword9";

        var set = await _owner.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = _customerId.ToString(), password = chosen });
        set.EnsureSuccessStatusCode();

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { identity = Phone, password = chosen });

        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Setting_a_password_ends_the_sessions_open_on_the_old_one()
    {
        var before = await StampAsync();

        var set = await _owner.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = _customerId.ToString(), password = "replacementPassword77" });
        set.EnsureSuccessStatusCode();

        // If this is being used because somebody else is in the account,
        // leaving them signed in defeats the point of using it.
        Assert.NotEqual(before, await StampAsync());
    }

    [Fact]
    public async Task An_operator_cannot_set_a_password_the_shop_would_refuse()
    {
        var set = await _owner.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = _customerId.ToString(), password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, set.StatusCode);
    }

    [Fact]
    public async Task An_unknown_customer_is_a_not_found_rather_than_a_fault()
    {
        var set = await _owner.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = Guid.NewGuid().ToString(), password = "aVeryLongEnoughPassword1" });

        Assert.Equal(HttpStatusCode.NotFound, set.StatusCode);
    }

    // --- who may do it ------------------------------------------------------

    [Fact]
    public async Task Neither_control_is_open_to_an_operator_below_owner()
    {
        Guid supportId = default;
        await _factory.WithDbAsync(async db =>
            supportId = (await TestData.AddAdminAsync(db, AdminRole.Support, "suspension-support@bojan.test")).Id);

        var support = _factory.CreateAdminClient(supportId);

        var blocking = await support.PostAsJsonAsync(
            "/api/admin/customers/block",
            new { customerId = _customerId.ToString(), blocked = true });

        var setting = await support.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = _customerId.ToString(), password = "aVeryLongEnoughPassword1" });

        // Closing somebody out of their account and setting the credential to
        // it are not things to hand to whoever is answering the support queue.
        Assert.Equal(HttpStatusCode.Forbidden, blocking.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, setting.StatusCode);
        Assert.False(await IsBlockedAsync());
    }

    // --- the trail ----------------------------------------------------------

    [Fact]
    public async Task Both_actions_leave_an_audit_line_and_the_password_is_not_in_it()
    {
        (await Block(true)).EnsureSuccessStatusCode();

        var set = await _owner.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = _customerId.ToString(), password = "auditedPassword123" });
        set.EnsureSuccessStatusCode();

        List<string> actions = [];
        List<string> targets = [];
        await _factory.WithDbAsync(async db =>
        {
            var rows = await db.AuditEntries.AsNoTracking().ToListAsync();
            actions = [.. rows.Select(r => r.Action)];
            targets = [.. rows.Select(r => r.Target)];
        });

        Assert.Contains("customer.blocked", actions);
        Assert.Contains("customer.password.set", actions);
        // The phone, not the password, and not a fragment of it.
        Assert.DoesNotContain(targets, t => t.Contains("auditedPassword", StringComparison.Ordinal));
    }
}
