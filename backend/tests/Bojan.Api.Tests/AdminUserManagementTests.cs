using Microsoft.Extensions.DependencyInjection;
using Bojan.Application.Auth;
using Bojan.Domain.Customers;
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

    /// <summary>
    /// Registers a shop account, then promotes it.
    /// </summary>
    /// <remarks>
    /// Two steps because that is now the only way an operator comes to exist:
    /// the panel appoints somebody who already shops here, and has no route
    /// that mints an account. A helper that posted a name and a password to the
    /// save endpoint — which is what this was — asks for somebody who has never
    /// registered, and is answered «no-such-account».
    /// </remarks>
    private async Task<Guid> CreateAsync(
        string email,
        string role = "support",
        string? phone = null,
        string password = Password)
    {
        var number = phone ?? TestData.PhoneFor(email);

        await _factory.WithDbAsync(async db =>
        {
            if (await db.Customers.AnyAsync(c => c.Phone == number)) return;

            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            db.Customers.Add(new Customer
            {
                Phone = number,
                Email = email,
                FirstName = "اپراتور",
                LastName = "تازه",
                PasswordHash = hasher.Hash(password),
            });

            await db.SaveChangesAsync();
        });

        var response = await Save(new { identity = number, role });
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

    /// <summary>The password hash on the shop account behind an operator.</summary>
    private async Task<string?> PasswordHashOf(Guid adminId)
    {
        string? hash = null;
        await _factory.WithDbAsync(async db =>
        {
            var admin = await db.AdminUsers.FirstAsync(a => a.Id == adminId);
            hash = (await db.Customers.FirstAsync(c => c.Id == admin.CustomerId)).PasswordHash;
        });
        return hash;
    }

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

    /* Two tests stood here, both about the «must change password» flag: that a
       newly created operator carried it, and that changing the password cleared
       it.

       Neither has anything left to assert. The flag existed for the window
       between an owner typing a password for somebody and that person replacing
       it, and an operator is no longer given a password at all — they are
       promoted from an account they already had, with a password only they have
       ever known. There is no window, so there is no flag. */

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

    /* The number conflict is now the account conflict: a phone identifies one
       shop account, and one account holds at most one grant. Appointing the
       same person twice is «already-an-operator», which
       An_account_cannot_hold_two_grants covers at the index that enforces it. */


    /* Two tests about the password posted when appointing an operator — that a
       weak one was refused, and that one below the floor was — have gone with
       the field itself. Appointing somebody takes an identity and a role; the
       password is theirs and was set when they registered, where the
       storefront's own policy already refuses a weak one. */


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

    /* An address that could never sign in is no longer something this endpoint
       can be handed: it takes the identity of an account that already exists,
       so a malformed one finds nothing and is answered «no-such-account»
       instead of being validated for shape. */


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

        // The hash to watch is the shop account's: that is where the operator's
        // one password lives, and it is what a save must not be able to touch.
        var before = await PasswordHashOf(id);

        var response = await Save(new { id = id.ToString(), password = "somethingElse123" });

        // Refused rather than ignored: a form that posted one and got a success
        // back would have every appearance of having set it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await PasswordHashOf(id));
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


    /* An owner used to be able to issue a replacement password for an
       operator who had locked themselves out, and this asserted that doing so
       ended their sessions and flagged the new password for replacing.

       The route is gone, and deliberately: the credential is that person's own
       shop account, so the way back in is the storefront's password-reset mail,
       which reaches them and nobody else. An owner who could set a password
       could then sign in as that operator, which is the hole a single
       credential closes. */




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

        var clearing = await support.PostAsJsonAsync(
            "/api/admin/settings/users/two-factor",
            new { id = target.ToString() });

        Assert.Equal(HttpStatusCode.Forbidden, creating.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, editing.StatusCode);
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

        // There is no password action left to check for, and that is the
        // stronger version of what this test was asserting: the trail cannot
        // leak a credential this endpoint never handles.
        Assert.DoesNotContain(targets, t => t.Contains(Password, StringComparison.Ordinal));
    }

    // --- granting ------------------------------------------------------------

    private async Task<List<string>> GrantsOf(Guid adminId)
    {
        List<string> grants = [];
        await _factory.WithDbAsync(async db =>
            grants = await db.AdminUserSections
                .AsNoTracking()
                .Where(s => s.AdminUserId == adminId)
                .Select(s => s.Section)
                .OrderBy(s => s)
                .ToListAsync());
        return grants;
    }

    /// <summary>
    /// The bug this screen was reported for: every grant made by editing an
    /// existing operator failed.
    /// </summary>
    /// <remarks>
    /// A grant row appended to the loaded collection arrived at the change
    /// tracker with the <c>Guid</c> its constructor had already given it, and a
    /// child with a key EF takes for one that exists — so the save came out as
    /// an <c>UPDATE</c> against a row that had never been inserted, affected
    /// nothing, and surfaced as «این مقدار تکراری است». Creating worked, because
    /// the whole graph is added at once; only editing was broken, which is
    /// exactly the half an owner uses after the first day.
    /// </remarks>
    [Fact]
    public async Task Granting_sections_to_an_existing_operator_is_saved()
    {
        var id = await CreateAsync("granted@bojan.test");

        var response = await Save(new
        {
            id = id.ToString(),
            role = "support",
            sections = new[] { PanelSection.Orders, PanelSection.Support },
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal(["orders", "support"], await GrantsOf(id));
    }

    [Fact]
    public async Task Saving_the_same_grants_twice_changes_nothing_and_still_succeeds()
    {
        var id = await CreateAsync("granted-twice@bojan.test");
        var body = new { id = id.ToString(), sections = new[] { PanelSection.Orders } };

        (await Save(body)).EnsureSuccessStatusCode();
        (await Save(body)).EnsureSuccessStatusCode();

        Assert.Equal(["orders"], await GrantsOf(id));
    }

    [Fact]
    public async Task A_single_screen_can_be_granted_without_the_section_around_it()
    {
        var id = await CreateAsync("returns-only@bojan.test");

        (await Save(new { id = id.ToString(), sections = new[] { "/returns" } }))
            .EnsureSuccessStatusCode();

        // Returns and orders are one section, so this is the grant that could
        // not be expressed at all before screens became grantable.
        Assert.Equal(["/returns"], await GrantsOf(id));
    }

    [Fact]
    public async Task A_whole_section_swallows_the_screens_inside_it()
    {
        var id = await CreateAsync("whole-section@bojan.test");

        (await Save(new
        {
            id = id.ToString(),
            sections = new[] { PanelSection.Orders, "/returns", "/invoices" },
        })).EnsureSuccessStatusCode();

        // Storing both would be the same grant twice, and a revoke that took
        // the section while a screen row survived would look like a revoke that
        // did nothing.
        Assert.Equal(["orders"], await GrantsOf(id));
    }

    [Fact]
    public async Task A_key_that_names_no_section_and_no_screen_is_dropped()
    {
        var id = await CreateAsync("bad-key@bojan.test");

        (await Save(new
        {
            id = id.ToString(),
            sections = new[] { PanelSection.Orders, "/there/is/no/such/screen", "سفارش‌ها" },
        })).EnsureSuccessStatusCode();

        Assert.Equal(["orders"], await GrantsOf(id));
    }

    [Fact]
    public async Task Clearing_every_box_leaves_the_operator_unnarrowed()
    {
        var id = await CreateAsync("cleared@bojan.test");
        (await Save(new { id = id.ToString(), sections = new[] { PanelSection.Orders } }))
            .EnsureSuccessStatusCode();

        (await Save(new { id = id.ToString(), sections = Array.Empty<string>() }))
            .EnsureSuccessStatusCode();

        Assert.Empty(await GrantsOf(id));
    }

    [Fact]
    public async Task A_save_that_does_not_carry_the_checklist_leaves_the_grants_alone()
    {
        var id = await CreateAsync("renamed@bojan.test");
        (await Save(new { id = id.ToString(), sections = new[] { PanelSection.Support } }))
            .EnsureSuccessStatusCode();

        // Correcting a typo in somebody's name must not silently revoke their
        // permissions — which is why omitting the field and sending an empty
        // list have to mean different things.
        (await Save(new { id = id.ToString(), name = "نام تازه" })).EnsureSuccessStatusCode();

        Assert.Equal(["support"], await GrantsOf(id));
    }

    /// <summary>
    /// The owner reaches everything and is not narrowable, so there is nothing
    /// to store and nothing to rotate their session over.
    /// </summary>
    [Fact]
    public async Task An_owner_cannot_be_narrowed()
    {
        var id = await CreateAsync("second-owner@bojan.test", role: "owner");
        var before = (await ReadAsync(id)).SecurityStamp;

        (await Save(new { id = id.ToString(), role = "owner", sections = new[] { PanelSection.Orders } }))
            .EnsureSuccessStatusCode();

        Assert.Empty(await GrantsOf(id));
        // Storing nothing must also mean signing nobody out: the owner editing
        // their own row was being logged out of the panel they stood in.
        Assert.Equal(before, (await ReadAsync(id)).SecurityStamp);
    }

    [Fact]
    public async Task Promoting_a_narrowed_operator_to_owner_drops_what_they_were_narrowed_to()
    {
        var id = await CreateAsync("promoted@bojan.test");
        (await Save(new { id = id.ToString(), sections = new[] { PanelSection.Support } }))
            .EnsureSuccessStatusCode();

        (await Save(new { id = id.ToString(), role = "owner", sections = new[] { PanelSection.Support } }))
            .EnsureSuccessStatusCode();

        // Otherwise a later demotion would silently restore a set of grants
        // chosen for a job they no longer do.
        Assert.Empty(await GrantsOf(id));
    }

    /// <summary>
    /// A cookie minted before a grant changed must not outlive it — which is
    /// the whole reason the panel is allowed to keep the list in one.
    /// </summary>
    [Fact]
    public async Task Changing_what_an_operator_may_open_ends_their_session()
    {
        var id = await CreateAsync("rotated@bojan.test");
        var before = (await ReadAsync(id)).SecurityStamp;

        (await Save(new { id = id.ToString(), sections = new[] { PanelSection.Orders } }))
            .EnsureSuccessStatusCode();

        Assert.NotEqual(before, (await ReadAsync(id)).SecurityStamp);
    }

    [Fact]
    public async Task Sign_in_reports_what_the_operator_may_open()
    {
        var id = await CreateAsync("reported@bojan.test");
        (await Save(new { id = id.ToString(), sections = new[] { "/returns" } }))
            .EnsureSuccessStatusCode();

        var login = await SignIn("reported@bojan.test", Password);
        login.EnsureSuccessStatusCode();

        // The panel draws its menu from this and leaves out the rest, so a
        // sign-in that did not carry it would show a narrowed operator every
        // screen in the panel and refuse them one by one.
        var body = await login.Content.ReadFromJsonAsync<SignedInOperator>();
        Assert.Equal(["/returns"], body!.Sections);
    }

    private sealed record SignedInOperator(IReadOnlyList<string>? Sections);
}
