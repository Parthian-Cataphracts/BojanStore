using Bojan.Domain.Common;

namespace Bojan.Domain.Admin;

/// <summary>
/// A panel operator.
/// </summary>
/// <remarks>
/// Sign-in is identity (phone or email) + password, unlike the storefront's
/// phone + SMS code — see <c>apps/admin/src/app/api/admin-auth/login/route.ts</c>,
/// which already calls <c>POST /auth/login</c> with
/// <c>{ identity, password }</c> and expects
/// <c>{ id, name, email, role, requiresTwoFactor? }</c> back.
/// </remarks>
public sealed class AdminUser : Entity
{
    /// <summary>
    /// The shop account this operator signs in as, on both sides.
    /// </summary>
    /// <remarks>
    /// A link rather than a merge, which is what keeps the two foreign-key
    /// graphs where they are: this row is named by audit entries, API keys,
    /// stock movements and issued quotes, and a customer is named by every
    /// order. One table would have meant moving both sets at once.
    ///
    /// Required, because a grant with nobody to grant it to is exactly the
    /// state that produced an owner who could not shop in their own shop.
    /// </remarks>
    public required Guid CustomerId { get; set; }

    /// <summary>Display name and contact, copied from the customer at grant time.</summary>
    /// <remarks>
    /// Denormalised deliberately, the way an audit row keeps the actor's name:
    /// the panel lists operators constantly and should not join to the customer
    /// table to print a heading. The customer record stays the truth.
    /// </remarks>
    public required string Name { get; set; }

    public required string Email { get; set; }

    /// <summary>Optional — an operator may sign in with either this or <see cref="Email"/>.</summary>
    public string? Phone { get; set; }

    public required AdminRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public bool TwoFactorEnabled { get; set; }

    /// <summary>TOTP secret, present only once two-factor is set up (screen 153).</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>
    /// The panel sections this operator may open.
    /// </summary>
    /// <remarks>
    /// Per operator, not per role. It was a grid of role against section, so
    /// granting one salesperson the returns queue granted it to every
    /// salesperson: the permission belonged to the job title rather than to the
    /// person doing it, and there was no way to say «this one, not that one».
    ///
    /// Empty means unnarrowed — the role's own reach — which is what an
    /// installation that has never opened the permissions screen should get.
    /// An owner is never consulted here at all: a panel whose full-access role
    /// can be locked out of a section is one click from unmanageable.
    /// </remarks>
    public ICollection<AdminUserSection> Sections { get; init; } = new List<AdminUserSection>();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    /// <summary>
    /// Changes whenever every existing session for this operator must stop
    /// working — the same device <c>Customer.SecurityStamp</c> plays on the
    /// storefront.
    /// </summary>
    /// <remarks>
    /// The panel's cookie is signed and self-contained and lasts a working day,
    /// so without this an operator who changed their password because it had
    /// been seen over their shoulder left the watcher's session open for the
    /// rest of that day. Role and <see cref="IsActive"/> were already read from
    /// this table on every request; the password was the one change nothing
    /// could reach.
    /// </remarks>
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();

    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
}

/// <summary>
/// One panel section a single operator has been granted.
/// </summary>
/// <remarks>
/// Existence is the grant, as it was for the role grid this replaces. The key
/// is the stable English one (<c>orders</c>, <c>products</c>, …), never the
/// Persian label — the grid before this stored the label, so editing the text
/// of one component silently invalidated everybody's permissions.
/// </remarks>
public sealed class AdminUserSection : Entity
{
    public required Guid AdminUserId { get; init; }

    /// <summary>A <c>PanelSection</c> key.</summary>
    public required string Section { get; init; }
}
