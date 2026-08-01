namespace Bojan.Domain.Admin;

/// <summary>
/// Panel roles.
/// </summary>
/// <remarks>
/// Must stay exactly these four values, in this casing once serialised — the
/// frontend's <c>AdminRole</c> type in
/// <c>apps/admin/src/lib/auth/session.ts</c> is
/// <c>'owner' | 'product' | 'sales' | 'support'</c>, and every resource's
/// allow-listed roles in <c>apps/admin/src/lib/api/resources.ts</c> are
/// written against those exact strings. Adding a fifth role here without
/// updating that file locks operators out of nothing — the frontend simply
/// never offers it.
/// </remarks>
public enum AdminRole
{
    Owner,
    Product,
    Sales,
    Support,
}
