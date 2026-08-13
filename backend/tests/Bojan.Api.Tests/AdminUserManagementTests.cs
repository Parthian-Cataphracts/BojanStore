using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Appointing operators, and managing the ones there are — screen 145's writes.
/// </summary>
/// <remarks>
/// <para>
/// The panel could only read this table. Nothing created an operator, so a shop
/// that needed a second person in the panel had exactly one way to get one:
/// hand over the owner's password. These cover the route that replaces that,
/// and — mostly — the four ways it could make the panel worse than it was.
/// </para>
/// <para>
/// Most of them are guards rather than the writes themselves. The writes are a
/// row and a hash; the guards are what stops this screen being the way a panel
/// ends up with no owner, or the way a stolen session promotes itself.
/// </para>
/// </remarks>
public sealed class AdminUserManagementTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _owner = null!;
    private Guid _ownerId;

    /// <summary>Twelve or more — the floor <c>POST /me/password</c> applies.</summary>
    private const string Password = "initialPassword123";

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
            _ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "operators-owner@bojan.test")).Id);

        _owner = _factory.CreateAdminClient(_ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    // --- helpers -------------------------------------------------------------

    private Task<HttpResponseMessage> Save(object body) =>
        _owner.PostAsJsonAsync("/api/admin/settings/users", body);

    private async Task<Guid> CreateAsync(
        string email,
        string role = "support",
        string? phone = null,
        string password = Password)
    {
        var response = await Save(new { name = "اپراتور تازه", email, phone, role, password });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreatedId>();
        return Guid.Parse(created!.Id);
    }

    private async Task<AdminUser> ReadAsync(Guid id)
    {
        AdminUser user = null!;
        await _factory.WithDbAsync(async db =>
            user = await db.AdminUsers.AsNoTracking().SingleAsync(a => a.Id == id));
        return user;
    }

    private Task<HttpResponseMessage> SignIn(string identity, string password) =>
        _factory.CreateClient().PostAsJsonAsync(
            "/api/admin/auth/login",
            new { identity, password });

    private sealed record CreatedId(string Id);

    private sealed record LoginBody(string? Token, bool? RequiresTwoFactor, bool? MustChangePassword);

    // --- appointing ----------------------------------------------------------

    [Fact]
    public async Task A_created_operator_signs_in_with_the_password_the_owner_typed()
    {
        await CreateAsync("appointed@bojan.test");

        var login = await SignIn("appointed@bojan.test", Password);

        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    /// <summary>
    /// The whole reason the flag exists: between the account being made and the
    /// operator's first sign-in, the password is known to two people, and the
    /// one who does not own it has no reason to stop knowing it.
    /// </summary>
    [Fact]
    public async Task The_initial_password_is_flagged_for_replacing_and_the_sign_in_says_so()
    {
        var id = await CreateAsync("must-change@bojan.test");

        Assert.True((await ReadAsync(id)).MustChangePassword);

        var login = await SignIn("must-change@bojan.test", Password);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();

        Assert.True(body!.MustChangePassword);
    }

    [Fact]
    public async Task Changing_the_password_yourself_is_what_clears_the_flag()
    {
        var id = await CreateAsync("clears-flag@bojan.test");
        var newcomer = _factory.CreateAdminClient(id);

        var changed = await newcomer.PostAsJsonAsync(
            "/api/admin/me/password",
            new { currentPassword = Password, newPassword = "theirOwnPassword456" });
        changed.EnsureSuccessStatusCode();

        var stored = await ReadAsync(id);
        Assert.False(stored.MustChangePassword);

        // And the sign-in stops saying it, so the panel stops holding them on
        // the change-password screen.
        var login = await SignIn("clears-flag@bojan.test", "theirOwnPassword456");
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        Assert.Null(body!.MustChangePassword);
    }

    [Fact]
    public async Task The_role_asked_for_is_the_role_stored()
    {
        var id = await CreateAsync("a-product-manager@bojan.test", role: "product");

        Assert.Equal(AdminRole.Product, (await ReadAsync(id)).Role);
    }

    [Fact]
    public async Task An_operator_may_be_created_with_a_phone_and_sign_in_with_it()
    {
        await CreateAsync("by-phone@bojan.test", phone: "09121110000");

        var login = await SignIn("09121110000", Password);

        login.EnsureSuccessStatusCode();
    }

    // --- what a create refuses ------------------------------------------------

    [Fact]
    public async Task An_address_another_operator_already_answers_to_is_a_conflict()
    {
        await CreateAsync("taken@bojan.test");

        var again = await Save(new
        {
            name = "دیگری",
            email = "taken@bojan.test",
            role = "sales",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>
    /// The unique index is case-sensitive and the sign-in lookup is not, so
    /// without folding the case these are two rows the database accepts and one
    /// identity the login resolves arbitrarily.
    /// </summary>
    [Fact]
    public async Task The_same_address_in_another_case_is_the_same_address()
    {
        await CreateAsync("sara@bojan.test");

        var again = await Save(new
        {
            name = "سارا",
            email = "Sara@Bojan.Test",
            role = "support",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_number_another_operator_already_answers_to_is_a_conflict()
    {
        await CreateAsync("first-number@bojan.test", phone: "09121110001");

        var again = await Save(new
        {
            name = "دیگری",
            email = "second-number@bojan.test",
            phone = "09121110001",
            role = "support",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_password_the_panel_would_refuse_is_refused_here_too()
    {
        var response = await Save(new
        {
            name = "کوتاه",
            email = "short-password@bojan.test",
            role = "support",
            password = "short1",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_role_that_is_not_a_role_is_refused_rather_than_defaulted()
    {
        var response = await Save(new
        {
            name = "نقش نامعلوم",
            email = "unknown-role@bojan.test",
            role = "superuser",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Email is a sign-in identity. An account created with something that is
    /// not one is an account the login screen will not accept, and the failure
    /// surfaces at the person who cannot get in rather than at the owner who
    /// mistyped it.
    /// </summary>
    [Fact]
    public async Task An_address_that_could_never_sign_in_is_refused()
    {
        var response = await Save(new
        {
            name = "بدون ایمیل",
            email = "sara",
            role = "support",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_number_that_is_not_a_mobile_number_is_refused()
    {
        var response = await Save(new
        {
            name = "شماره غلط",
            email = "bad-phone@bojan.test",
            phone = "12345",
            role = "support",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- editing --------------------------------------------------------------

    [Fact]
    public async Task A_role_change_takes_effect_and_ends_the_sessions_signed_under_the_old_one()
    {
        var id = await CreateAsync("promoted@bojan.test");
        var before = (await ReadAsync(id)).SecurityStamp;

        (await Save(new { id = id.ToString(), role = "sales" })).EnsureSuccessStatusCode();

        var stored = await ReadAsync(id);
        Assert.Equal(AdminRole.Sales, stored.Role);
        // The panel's own cookie carries the role it was signed with for a
        // working day; without the rotation a demoted operator keeps being shown
        // screens whose every request is then refused.
        Assert.NotEqual(before, stored.SecurityStamp);
    }

    [Fact]
    public async Task Suspending_ends_open_sessions_and_closes_the_api_to_them()
    {
        var id = await CreateAsync("suspended-operator@bojan.test");
        var theirs = _factory.CreateAdminClient(id);

        (await Save(new { id = id.ToString(), isActive = false })).EnsureSuccessStatusCode();

        Assert.False((await ReadAsync(id)).IsActive);

        // The client was built with the stamp from before, which is the state a
        // browser holding a session cookie is in.
        var afterwards = await theirs.GetAsync("/api/admin/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task Reinstating_puts_them_back()
    {
        var id = await CreateAsync("reinstated@bojan.test");
        (await Save(new { id = id.ToString(), isActive = false })).EnsureSuccessStatusCode();

        (await Save(new { id = id.ToString(), isActive = true })).EnsureSuccessStatusCode();

        Assert.True((await ReadAsync(id)).IsActive);
    }

    [Fact]
    public async Task Renaming_somebody_does_not_sign_them_out()
    {
        var id = await CreateAsync("renamed@bojan.test");
        var before = (await ReadAsync(id)).SecurityStamp;

        (await Save(new { id = id.ToString(), name = "نام تازه" })).EnsureSuccessStatusCode();

        var stored = await ReadAsync(id);
        Assert.Equal("نام تازه", stored.Name);
        // Correcting a typo in a colleague's name is not a security event, and
        // treating it as one would teach operators that being signed out means
        // nothing.
        Assert.Equal(before, stored.SecurityStamp);
    }

    [Fact]
    public async Task A_phone_can_be_cleared_by_sending_an_empty_one()
    {
        var id = await CreateAsync("clearing-phone@bojan.test", phone: "09121110002");

        (await Save(new { id = id.ToString(), phone = "" })).EnsureSuccessStatusCode();

        Assert.Null((await ReadAsync(id)).Phone);
    }

    [Fact]
    public async Task Taking_another_operators_address_is_a_conflict()
    {
        await CreateAsync("holder@bojan.test");
        var other = await CreateAsync("mover@bojan.test");

        var response = await Save(new { id = other.ToString(), email = "holder@bojan.test" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Nothing about "save my own row unchanged" should trip the uniqueness
    /// check on the address that row already holds.
    /// </summary>
    [Fact]
    public async Task Saving_an_operator_with_their_own_address_is_not_a_conflict()
    {
        var id = await CreateAsync("unchanged@bojan.test");

        var response = await Save(new { id = id.ToString(), email = "unchanged@bojan.test", name = "همان" });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_password_cannot_be_slipped_into_a_save()
    {
        var id = await CreateAsync("no-password-here@bojan.test");
        var before = (await ReadAsync(id)).PasswordHash;

        var response = await Save(new { id = id.ToString(), password = "somethingElse123" });

        // Refused rather than ignored: a form that posted one and got a success
        // back would have every appearance of having set it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, (await ReadAsync(id)).PasswordHash);
    }

    [Fact]
    public async Task An_unknown_operator_is_a_not_found_rather_than_a_fault()
    {
        var response = await Save(new { id = Guid.NewGuid().ToString(), name = "کسی" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- the two ways to lock everyone out ------------------------------------

    [Fact]
    public async Task An_operator_cannot_suspend_themselves()
    {
        var response = await Save(new { id = _ownerId.ToString(), isActive = false });

        // Instant and total: it ends the session doing it, and the screen that
        // could undo it is one only somebody else can now reach.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((await ReadAsync(_ownerId)).IsActive);
    }

    [Fact]
    public async Task An_operator_may_still_rename_themselves()
    {
        var response = await Save(new { id = _ownerId.ToString(), name = "مالک فروشگاه" });

        response.EnsureSuccessStatusCode();
        Assert.Equal("مالک فروشگاه", (await ReadAsync(_ownerId)).Name);
    }

    /// <summary>
    /// Settings, the permission grid and this very screen are owner-only, so a
    /// panel with no owner cannot appoint its own way out of being one.
    /// </summary>
    [Fact]
    public async Task The_last_owner_cannot_step_down()
    {
        var response = await Save(new { id = _ownerId.ToString(), role = "sales" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AdminRole.Owner, (await ReadAsync(_ownerId)).Role);
    }

    /// <summary>
    /// The counterpart, and the reason the rule is about the last owner rather
    /// than about owners: an owner handing the shop over has to be able to step
    /// down once their successor exists.
    /// </summary>
    [Fact]
    public async Task An_owner_may_step_down_once_a_successor_exists()
    {
        await CreateAsync("successor@bojan.test", role: "owner");

        var response = await Save(new { id = _ownerId.ToString(), role = "product" });

        response.EnsureSuccessStatusCode();
        Assert.Equal(AdminRole.Product, (await ReadAsync(_ownerId)).Role);
    }

    [Fact]
    public async Task Demoting_another_owner_is_fine_while_an_active_one_remains()
    {
        var second = await CreateAsync("spare-owner@bojan.test", role: "owner");

        var response = await Save(new { id = second.ToString(), role = "support" });

        response.EnsureSuccessStatusCode();
        Assert.Equal(AdminRole.Support, (await ReadAsync(second)).Role);
    }

    [Fact]
    public async Task The_last_active_owner_cannot_be_suspended_by_another_owner()
    {
        // The caller steps back so `second` is the only active owner, then
        // reaches for the suspension — which is the shape the count exists for
        // and the one the self rule above does not cover.
        var second = await CreateAsync("sole-owner@bojan.test", role: "owner");
        var secondClient = _factory.CreateAdminClient(second);

        (await secondClient.PostAsJsonAsync(
            "/api/admin/settings/users",
            new { id = _ownerId.ToString(), role = "sales" })).EnsureSuccessStatusCode();

        // Back to the panel as the demoted operator would arrive: `second` is
        // now the only owner, and the only client that may ask.
        var response = await secondClient.PostAsJsonAsync(
            "/api/admin/settings/users",
            new { id = second.ToString(), isActive = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((await ReadAsync(second)).IsActive);
    }

    /// <summary>
    /// A suspended owner cannot sign in, so counting them would let the panel
    /// arrive at nobody who can open it while the rule reported two.
    /// </summary>
    [Fact]
    public async Task A_suspended_owner_does_not_count_as_one_still_holding_the_panel()
    {
        var dormant = await CreateAsync("dormant-owner@bojan.test", role: "owner");
        (await Save(new { id = dormant.ToString(), isActive = false })).EnsureSuccessStatusCode();

        var response = await Save(new { id = _ownerId.ToString(), role = "sales" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AdminRole.Owner, (await ReadAsync(_ownerId)).Role);
    }

    // --- setting somebody else's password -------------------------------------

    [Fact]
    public async Task The_password_an_owner_sets_is_the_one_that_signs_in()
    {
        var id = await CreateAsync("forgot-theirs@bojan.test");

        var set = await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/password",
            new { id = id.ToString(), password = "ownerIssuedPass77" });
        set.EnsureSuccessStatusCode();

        var login = await SignIn("forgot-theirs@bojan.test", "ownerIssuedPass77");
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Setting_a_password_ends_open_sessions_and_asks_for_a_replacement()
    {
        var id = await CreateAsync("reset-sessions@bojan.test");
        // Cleared first, so the assertion below is about this reset rather than
        // about the flag the account was created with.
        var theirs = _factory.CreateAdminClient(id);
        (await theirs.PostAsJsonAsync(
            "/api/admin/me/password",
            new { currentPassword = Password, newPassword = "chosenByThem123" })).EnsureSuccessStatusCode();

        var before = (await ReadAsync(id)).SecurityStamp;

        (await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/password",
            new { id = id.ToString(), password = "ownerIssuedPass88" })).EnsureSuccessStatusCode();

        var stored = await ReadAsync(id);
        Assert.NotEqual(before, stored.SecurityStamp);
        Assert.True(stored.MustChangePassword);
    }

    /// <summary>
    /// The self-service route asks for the current password first, and that is
    /// the whole thing standing between a stolen session and the account under
    /// it. A route here that skipped it would hand the account over.
    /// </summary>
    [Fact]
    public async Task An_operator_cannot_set_their_own_password_through_this_route()
    {
        var response = await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/password",
            new { id = _ownerId.ToString(), password = "sidestepping123" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_password_below_the_floor_is_refused()
    {
        var id = await CreateAsync("floor@bojan.test");

        var response = await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/password",
            new { id = id.ToString(), password = "short1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- the lost authenticator ------------------------------------------------

    [Fact]
    public async Task Clearing_a_second_factor_lets_the_password_alone_sign_them_in_again()
    {
        var id = await CreateAsync("lost-phone@bojan.test");

        await _factory.WithDbAsync(async db =>
        {
            var user = await db.AdminUsers.SingleAsync(a => a.Id == id);
            user.TwoFactorEnabled = true;
            user.TwoFactorSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
            await db.SaveChangesAsync();
        });

        // Locked out: the password alone answers with a challenge and no token.
        var blocked = await (await SignIn("lost-phone@bojan.test", Password))
            .Content.ReadFromJsonAsync<LoginBody>();
        Assert.True(blocked!.RequiresTwoFactor);

        (await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/two-factor",
            new { id = id.ToString() })).EnsureSuccessStatusCode();

        var stored = await ReadAsync(id);
        Assert.False(stored.TwoFactorEnabled);
        // Cleared rather than kept beside a false flag: enrolling again starts
        // from a new QR code, because a secret that has been off is one whose
        // screenshots have had time to go somewhere.
        Assert.Null(stored.TwoFactorSecret);

        var after = await (await SignIn("lost-phone@bojan.test", Password))
            .Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrWhiteSpace(after!.Token));
    }

    [Fact]
    public async Task An_operator_cannot_clear_their_own_second_factor_from_here()
    {
        var response = await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/two-factor",
            new { id = _ownerId.ToString() });

        // Their own is on the security screen, where turning it off costs a
        // current code — and a route that let a session strip the factor
        // guarding it would leave the factor protecting nothing but itself.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clearing_a_second_factor_nobody_has_is_not_an_error_and_writes_no_line()
    {
        var id = await CreateAsync("no-factor@bojan.test");

        (await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/two-factor",
            new { id = id.ToString() })).EnsureSuccessStatusCode();

        // Two owners on the same screen, or one clicking twice — but the trail
        // must not claim a factor was lifted when there was none.
        Assert.DoesNotContain("admin-user.2fa.cleared", await AuditActionsAsync());
    }

    // --- who may do any of it --------------------------------------------------

    [Fact]
    public async Task None_of_it_is_open_to_an_operator_below_owner()
    {
        var target = await CreateAsync("target@bojan.test");

        Guid supportId = default;
        await _factory.WithDbAsync(async db =>
            supportId = (await TestData.AddAdminAsync(db, AdminRole.Support, "operators-support@bojan.test")).Id);
        var support = _factory.CreateAdminClient(supportId);

        var creating = await support.PostAsJsonAsync(
            "/api/admin/settings/users",
            new { name = "خودم", email = "self-appointed@bojan.test", role = "owner", password = Password });

        var editing = await support.PostAsJsonAsync(
            "/api/admin/settings/users",
            new { id = supportId.ToString(), role = "owner" });

        var resetting = await support.PostAsJsonAsync(
            "/api/admin/settings/users/password",
            new { id = target.ToString(), password = "takingOver123" });

        var clearing = await support.PostAsJsonAsync(
            "/api/admin/settings/users/two-factor",
            new { id = target.ToString() });

        Assert.Equal(HttpStatusCode.Forbidden, creating.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, editing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, resetting.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clearing.StatusCode);
        Assert.Equal(AdminRole.Support, (await ReadAsync(supportId)).Role);
    }

    // --- the trail --------------------------------------------------------------

    private async Task<List<string>> AuditActionsAsync()
    {
        List<string> actions = [];
        await _factory.WithDbAsync(async db =>
            actions = await db.AuditEntries.AsNoTracking().Select(e => e.Action).ToListAsync());
        return actions;
    }

    [Fact]
    public async Task Every_action_leaves_a_line_and_no_password_is_in_any_of_them()
    {
        var id = await CreateAsync("audited@bojan.test");
        (await Save(new { id = id.ToString(), role = "sales" })).EnsureSuccessStatusCode();
        (await Save(new { id = id.ToString(), isActive = false })).EnsureSuccessStatusCode();
        (await _owner.PostAsJsonAsync(
            "/api/admin/settings/users/password",
            new { id = id.ToString(), password = "auditedPassword321" })).EnsureSuccessStatusCode();

        List<string> actions = [];
        List<string> targets = [];
        await _factory.WithDbAsync(async db =>
        {
            var rows = await db.AuditEntries.AsNoTracking().ToListAsync();
            actions = [.. rows.Select(r => r.Action)];
            targets = [.. rows.Select(r => r.Target)];
        });

        Assert.Contains("admin-user.created", actions);
        Assert.Contains("admin-user.updated", actions);
        Assert.Contains("admin-user.suspended", actions);
        Assert.Contains("admin-user.password.set", actions);
        Assert.DoesNotContain(targets, t => t.Contains("auditedPassword", StringComparison.Ordinal));
        Assert.DoesNotContain(targets, t => t.Contains(Password, StringComparison.Ordinal));
    }
}
