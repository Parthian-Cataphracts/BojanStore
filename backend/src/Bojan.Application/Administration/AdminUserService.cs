using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Domain.Admin;

namespace Bojan.Application.Administration;

/// <summary>
/// The operators themselves — screen 145's writes.
/// </summary>
/// <remarks>
/// <para>
/// Until now the panel could only read this table. An operator existed because
/// the seeder had put one there, and a shop that wanted a second one had no
/// route to it: the screen's "افزودن کاربر" button was disabled and said so,
/// because there was nothing for a form to post to. Every other way of getting
/// a colleague into the panel meant handing over the owner's own password.
/// </para>
/// <para>
/// Its own service rather than more of <see cref="AdminOperationsService"/>,
/// which already runs to a thousand lines of orders, support and settings.
/// What separates them is not size though: everything here writes the table
/// that decides who may write anything at all, so the guards below are the
/// whole of it and they belong somewhere they can be read in one sitting.
/// </para>
/// <para>
/// Nothing here deletes. <c>AdminUser</c> is the foreign key on audit entries,
/// API keys, stock movements and issued quotes, most of them
/// <c>DeleteBehavior.Restrict</c> — so removing an operator either fails at the
/// database or, if it did not, would take the trail of what they did with them.
/// Suspension is the answer instead: it closes every door the account has
/// while the history stays readable and still names the person who made it.
/// </para>
/// </remarks>
public sealed class AdminUserService(
    IAdminRepository repository,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IDateTimeProvider clock)
{
    /// <summary>
    /// Creates an operator, or edits one — the panel's forms post the same
    /// shape either way, and an id is what tells them apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A password is accepted only on the create branch. Changing one is
    /// a rescue by the owner, because it has to end the sessions
    /// open on the old password, and folding that into a save that also renames
    /// somebody would mean correcting a typo in a colleague's name signed them
    /// out of the panel.
    /// </para>
    /// <para>
    /// The initial password is one the owner types and then reads out to the
    /// person it belongs to, so it arrives already known to two people. It is
    /// marked <see cref="AdminUser.MustChangePassword"/> for exactly that
    /// reason — see the flag's own note.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult<string>> SaveAsync(
        Guid actorId,
        SaveAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        return string.IsNullOrEmpty(request.Id)
            ? await CreateAsync(name, email, phone, request, cancellationToken)
            : await UpdateAsync(actorId, name, email, phone, request, cancellationToken);
    }

    /// <summary>
    /// Appoints an operator: finds the shop account they already have, and
    /// grants it the panel.
    /// </summary>
    /// <remarks>
    /// It used to mint an account — a second e-mail and a password the owner
    /// typed and then read out to somebody. That produced two records for one
    /// person that nothing could tell apart, and a credential known to two
    /// people from the moment it existed.
    ///
    /// So there is no password here and no name: the person registered on the
    /// site, and both already belong to them. What this screen chooses is who,
    /// and what they may open.
    /// </remarks>
    private async Task<UseCaseResult<string>> CreateAsync(
        string? name,
        string? email,
        string? phone,
        SaveAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        // Whichever the owner typed; the field they used is not the point.
        var identity = (request.Identity ?? email ?? phone)?.Trim();
        if (string.IsNullOrEmpty(identity))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "identity");
        }

        // Required on create and only on create: an account with no role is not
        // a lesser operator, it is one no policy can place.
        if (WireFormat.ParseAdminRole(request.Role) is not { } role)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "role");
        }

        var customer = identity.Contains('@', StringComparison.Ordinal)
            ? await customers.FindByEmailAsync(identity.ToLowerInvariant(), cancellationToken)
            : await customers.FindByPhoneAsync(identity, cancellationToken);

        if (customer is null)
        {
            // Named rather than folded into a generic failure: the owner is
            // being told to send this person to register first, which is a
            // different instruction from "you typed it wrong".
            return UseCaseResult<string>.Failure(UseCaseError.NotFound, "no-such-account");
        }

        if (customer.PasswordHash is null)
        {
            // An account that has only ever signed in by SMS code has no
            // password to open the panel with, and the panel takes a password.
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "account-has-no-password");
        }

        if (await repository.FindAdminUserByCustomerAsync(customer.Id, cancellationToken) is not null)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Conflict, "already-an-operator");
        }

        var user = new AdminUser
        {
            CustomerId = customer.Id,
            Name = FullName(customer) is { Length: > 0 } known ? known : (name?.Trim() ?? customer.Phone),
            Email = customer.Email ?? string.Empty,
            Phone = customer.Phone,
            Role = role,
            IsActive = request.IsActive ?? true,
            CreatedAtUtc = clock.UtcNow,
        };

        foreach (var section in CleanSections(request.Sections))
        {
            user.Sections.Add(new AdminUserSection { AdminUserId = user.Id, Section = section });
        }

        repository.AddAdminUser(user);

        // Who was appointed and to what. The trail has never recorded a
        // credential and now there is not one here to record.
        audit.Record("admin-user.created", $"{user.Phone} ({WireFormat.AdminRole(role)})");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<string>.Success(user.Id.ToString());
    }

    private static string FullName(Domain.Customers.Customer customer) =>
        string.Join(' ', new[] { customer.FirstName, customer.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))).Trim();

    /// <summary>
    /// Keeps only sections this application actually has.
    /// </summary>
    /// <remarks>
    /// Validated here rather than trusted from the form, so a crafted body
    /// cannot store a key that no filter checks and no screen shows — a grant
    /// nobody can see is worse than no grant.
    /// </remarks>
    private static IReadOnlyList<string> CleanSections(IReadOnlyList<string>? requested) =>
        (requested ?? [])
            .Select(section => section.Trim().ToLowerInvariant())
            .Where(PanelSection.IsKnown)
            .Distinct()
            .ToList();


    private async Task<UseCaseResult<string>> UpdateAsync(
        Guid actorId,
        string? name,
        string? email,
        string? phone,
        SaveAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "id");
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            // Refused rather than ignored. A form that posted a password here
            // and got a success back would have every appearance of having set
            // one, and the operator would find out otherwise at the sign-in
            // screen of somebody who cannot reach them.
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "password-not-here");
        }

        if (await repository.FindAdminUserAsync(id, cancellationToken) is not { } user)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        var role = user.Role;
        if (request.Role is not null)
        {
            if (WireFormat.ParseAdminRole(request.Role) is not { } parsed)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "role");
            }

            role = parsed;
        }

        var isActive = request.IsActive ?? user.IsActive;

        // Suspending yourself is instant and total: it ends the session doing
        // it and closes the door it came through, and the screen that could
        // reopen it is one only somebody else can now reach. There is no shop
        // in which this is the thing an operator meant to press.
        if (actorId == id && !isActive)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Forbidden, "self-suspend");
        }

        // Settings, the permission grid and this screen are owner-only, so an
        // owner-less panel cannot appoint its own way out of being owner-less.
        // Stepping down is otherwise allowed, including for the caller: an owner
        // handing the shop over should be able to become a product manager once
        // their successor exists, and this is what "once" means.
        var losesOwner = user.Role == AdminRole.Owner && user.IsActive && (role != AdminRole.Owner || !isActive);
        if (losesOwner && await repository.CountActiveOwnersAsync(cancellationToken) <= 1)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Conflict, "last-owner");
        }

        if (name is not null)
        {
            if (name.Length == 0)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "name");
            }

            user.Name = name;
        }

        if (email is not null && !LooksLikeEmail(email))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "email");
        }

        var nextEmail = email ?? user.Email;
        // `phone` is null both when the field was omitted and when it was
        // cleared, so the request's own field decides which of those happened —
        // otherwise an operator could never remove a number they had typed.
        var nextPhone = request.Phone is null ? user.Phone : phone;

        if ((nextEmail != user.Email || nextPhone != user.Phone)
            && await repository.IsAdminIdentityTakenAsync(nextEmail, nextPhone, id, cancellationToken))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Conflict, "identity-taken");
        }

        var roleChanged = role != user.Role;
        var suspended = user.IsActive && !isActive;

        user.Email = nextEmail;
        user.Phone = nextPhone;
        user.Role = role;
        user.IsActive = isActive;

        /*
          Sections, when the request carries them.

          `null` means the form did not send the checklist and whatever is
          stored stands; an empty list means the owner cleared every box, which
          is «unnarrowed», not «locked out» — the filter reads no rows as the
          role's own reach. The two have to stay distinguishable or saving a
          name would silently revoke somebody's permissions.
        */
        if (request.Sections is not null)
        {
            var wanted = CleanSections(request.Sections);

            foreach (var stale in user.Sections.Where(s => !wanted.Contains(s.Section)).ToList())
            {
                user.Sections.Remove(stale);
            }

            foreach (var section in wanted.Where(s => user.Sections.All(existing => existing.Section != s)))
            {
                user.Sections.Add(new AdminUserSection { AdminUserId = user.Id, Section = section });
            }

            // What they may open changed, so a cookie minted before this must
            // not outlive it — the same reason a role change rotates.
            user.RotateSecurityStamp();
        }

        if (roleChanged || suspended)
        {
            // The API reads the role from this row on every request, so it
            // needs no help — but the panel's own cookie carries the role it
            // was signed with and lasts a working day. Without this a demoted
            // operator keeps being shown screens whose every request is then
            // refused, and a suspended one keeps being shown the panel at all.
            user.RotateSecurityStamp();
        }

        audit.Record(
            suspended ? "admin-user.suspended" : "admin-user.updated",
            $"{user.Email} ({WireFormat.AdminRole(role)})");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<string>.Success(user.Id.ToString());
    }

    /* An owner no longer sets anybody's password.

       There was a rescue here for an operator locked out with no self-service
       route back, and it is not needed: the credential is the person's own shop
       account, so the way back in is the storefront's «رمز را فراموش کرده‌ام»,
       which mails a link to them and to nobody else. Keeping this would have
       meant an owner who could set a password and then sign in as that
       operator — the exact hole the single credential closes. */


    /// <summary>
    /// Turns off another operator's second factor, without their code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the authenticator that was on a phone that is now at the bottom of a
    /// river. Without it that operator is locked out permanently and by design:
    /// the second factor is meant to be unbypassable, and the only way it can
    /// be both that and survivable is if somebody above them can lift it.
    /// </para>
    /// <para>
    /// Never the caller's own, deliberately. Their own is on the security
    /// screen, where turning it off costs a current code — and a route that let
    /// a session strip the second factor guarding it would leave the factor
    /// protecting nothing but itself.
    /// </para>
    /// <para>
    /// The secret is cleared rather than kept beside a false flag, so enrolling
    /// again starts from a new QR code. A secret that has been off is one whose
    /// backup codes and screenshots have had time to go somewhere.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult> ClearTwoFactorAsync(
        Guid actorId,
        AdminUserTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        if (id == actorId)
        {
            return UseCaseResult.Failure(UseCaseError.Forbidden, "use-own-security-screen");
        }

        if (await repository.FindAdminUserAsync(id, cancellationToken) is not { } user)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (!user.TwoFactorEnabled && user.TwoFactorSecret is null)
        {
            // Already off. Not an error — two owners on the same screen, or one
            // clicking twice — but it must not write a line claiming a second
            // factor was lifted when there was none.
            return UseCaseResult.Success();
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;

        // Whoever is holding a session on this account passed a factor that no
        // longer exists, and if the reason for this call is that somebody else
        // has the authenticator, that somebody is the one still signed in.
        user.RotateSecurityStamp();

        audit.Record("admin-user.2fa.cleared", user.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// One rule for what an operator's password must be, wherever it is set.
    /// </summary>
    /// <remarks>
    /// The customer's <c>PasswordPolicy</c> is not it: that floor is eight, and
    /// these accounts open the shop's books rather than one person's orders.
    /// The constant is <see cref="AdminOperationsService"/>'s so that setting a
    /// password for somebody and changing your own cannot come to disagree
    /// about the number — which is exactly the drift that put three different
    /// answers in three layers before.
    /// </remarks>

    /// <summary>
    /// The same shape the panel's sign-in form requires of the identity it is
    /// typed into.
    /// </summary>
    /// <remarks>
    /// Structural only — nothing here can tell whether anybody reads the
    /// address. It exists because email is a sign-in identity: an operator
    /// created with "sara" instead of "sara@bojan.com" has an account the login
    /// screen will not accept, and the failure surfaces at the person who
    /// cannot get in rather than at the owner who mistyped it.
    /// </remarks>
    private static bool LooksLikeEmail(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@'))
        {
            return false;
        }

        var domain = value[(at + 1)..];
        return domain.Length >= 3
            && domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !value.Any(char.IsWhiteSpace);
    }
}
