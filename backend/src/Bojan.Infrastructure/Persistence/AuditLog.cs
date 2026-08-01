using Bojan.Application.Common;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Persistence;

/// <summary>
/// Writes the audit row into the same change tracker as the change it
/// describes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Record"/> adds, it does not save. The caller's own
/// <see cref="IUnitOfWork.SaveChangesAsync"/> commits the change and its trail
/// together, so a successful write with no audit row cannot happen —
/// <c>BACKEND.md</c> Phase 7's requirement is enforced by the transaction, not
/// by every handler remembering to call a logger.
/// </para>
/// <para>
/// The actor's name is resolved once and cached for the request: a handful of
/// writes in one request would otherwise each go and look up the same operator.
/// </para>
/// </remarks>
public sealed class AuditLog(BojanDbContext db, ICurrentUser currentUser) : IAuditLog
{
    private string? _actorName;

    public void Record(string action, string target)
    {
        if (currentUser.AdminId is not { } actorId)
        {
            // Nothing to attribute it to. An audit row with no actor is worse
            // than none: it reads as an action someone took anonymously.
            return;
        }

        _actorName ??= db.AdminUsers
            .Where(admin => admin.Id == actorId)
            .Select(admin => admin.Name)
            .FirstOrDefault() ?? actorId.ToString();

        db.AuditEntries.Add(new AuditEntry
        {
            ActorId = actorId,
            ActorName = _actorName,
            Action = action,
            Target = target,
            Ip = currentUser.Ip,
        });
    }
}
