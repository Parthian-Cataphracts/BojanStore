using Bojan.Application.Auth;
using Bojan.Domain.Identity;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfEmailVerificationTokenStore(BojanDbContext db) : IEmailVerificationTokenStore
{
    public void Add(EmailVerificationToken token) => db.EmailVerificationTokens.Add(token);

    public Task<EmailVerificationToken?> FindActiveAsync(
        string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now,
            cancellationToken);

    /// <remarks>
    /// Counts rows created in the window, spent or not. Consuming a link must
    /// not hand back a slot: a customer who clicks one and asks for another has
    /// still had two sent, and forgiving the spent ones would make the ceiling
    /// depend on how fast they read their mail.
    /// </remarks>
    public async Task<EmailVerificationSendWindow> CountSentSinceAsync(
        Guid customerId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        // One grouped round trip rather than a count and then a min.
        var window = await db.EmailVerificationTokens
            .Where(t => t.CustomerId == customerId && t.CreatedAtUtc >= since)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Oldest = (DateTimeOffset?)g.Min(t => t.CreatedAtUtc) })
            .FirstOrDefaultAsync(cancellationToken);

        return new EmailVerificationSendWindow(window?.Count ?? 0, window?.Oldest);
    }

    public async Task InvalidateForCustomerAsync(
        Guid customerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var live = await db.EmailVerificationTokens
            .Where(t => t.CustomerId == customerId && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var token in live)
        {
            token.Consume(now);
        }
    }
}
