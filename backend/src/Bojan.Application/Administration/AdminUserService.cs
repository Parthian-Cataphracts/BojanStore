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
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock)
{
    /// <summary>
    /// Creates an operator, or edits one — the panel's forms post the same
    /// shape either way, and an id is what tells them apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A password is accepted only on the create branch. Changing one is
    /// <see cref="SetPasswordAsync"/>'s job, because it has to end the sessions
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

    private async Task<UseCaseResult<string>> CreateAsync(
        string? name,
        string? email,
        string? phone,
        SaveAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "name");
        }

        if (!LooksLikeEmail(email))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "email");
        }

        // Required on create and only on create: an account with no role is not
        // a lesser operator, it is one no policy can place.
        if (WireFormat.ParseAdminRole(request.Role) is not { } role)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "role");
        }

        if (PasswordTooShort(request.Password))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "password");
        }

        if (await repository.IsAdminIdentityTakenAsync(email!, phone, null, cancellationToken))
        {
            // Conflict rather than invalid: nothing about the request is
            // malformed, the address is simply somebody else's, and the panel
            // says so beside the field instead of failing at a unique index.
            return UseCaseResult<string>.Failure(UseCaseError.Conflict, "identity-taken");
        }

        var user = new AdminUser
        {
            Name = name,
            Email = email!,
            Phone = phone,
            PasswordHash = passwordHasher.Hash(request.Password!),
            Role = role,
            IsActive = request.IsActive ?? true,
            MustChangePassword = true,
            CreatedAtUtc = clock.UtcNow,
        };

        repository.AddAdminUser(user);

        // The address, never the password. The trail records that an operator
        // was appointed and to what, which is the part that has to survive.
        audit.Record("admin-user.created", $"{user.Email} ({WireFormat.AdminRole(role)})");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<string>.Success(user.Id.ToString());
    }

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

    /// <summary>
    /// Sets another operator's password — the answer for one who is locked out
    /// and has no self-service route back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not for the caller's own account, which has <c>POST /me/password</c>.
    /// The difference is the whole point of both: that one asks for the current
    /// password first, and a route that let a session reset its own credential
    /// without knowing it would hand a stolen cookie the account behind it.
    /// </para>
    /// <para>
    /// Every session the operator has open ends, which is usually why this is
    /// being reached for. The new password is marked as needing replacing, for
    /// the same reason the initial one is.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult> SetPasswordAsync(
        Guid actorId,
        AdminUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        if (id == actorId)
        {
            return UseCaseResult.Failure(UseCaseError.Forbidden, "use-own-password-screen");
        }

        if (PasswordTooShort(request.Password))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "password");
        }

        if (await repository.FindAdminUserAsync(id, cancellationToken) is not { } user)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        user.PasswordHash = passwordHasher.Hash(request.Password!);
        user.MustChangePassword = true;
        user.RotateSecurityStamp();

        audit.Record("admin-user.password.set", user.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

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
    private static bool PasswordTooShort(string? password) =>
        (password?.Length ?? 0) < AdminOperationsService.MinimumPasswordLength;

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
