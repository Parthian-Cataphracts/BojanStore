using Bojan.Application.Auth;
using Bojan.Domain.Identity;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfPasswordResetTokenStore(BojanDbContext db) : IPasswordResetTokenStore
{
    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);

    /// <summary>
    /// Filtered in the database, not after loading.
    /// </summary>
    /// <remarks>
    /// The expiry and the consumed check belong in the query so an expired or
    /// spent token is simply not found — the caller gets one "no" for all three
    /// cases without having to remember to test for them.
    /// </remarks>
    public Task<PasswordResetToken?> FindActiveAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now,
            cancellationToken);

    /// <summary>
    /// Marks every outstanding token for the customer as spent.
    /// </summary>
    /// <remarks>
    /// Loaded and mutated rather than issued as one <c>ExecuteUpdate</c>: the
    /// rows have to move through the same change tracker as the password write
    /// that prompted this, so a single SaveChanges commits both or neither.
    /// There are never more than a handful.
    /// </remarks>
    public async Task InvalidateAllAsync(Guid customerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var outstanding = await db.PasswordResetTokens
            .Where(t => t.CustomerId == customerId && t.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in outstanding)
        {
            token.Consume(now);
        }
    }
}
