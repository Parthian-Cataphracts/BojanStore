using Bojan.Application.Auth;
using Bojan.Domain.Identity;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfOtpChallengeStore(BojanDbContext db) : IOtpChallengeStore
{
    public async Task<OtpChallenge> CreateAsync(
        string phone,
        string codeHash,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        // A new request supersedes whatever was pending — the shopper who
        // asks for a second code has abandoned the first one.
        var existing = await db.OtpChallenges.Where(c => c.Phone == phone).ToListAsync(cancellationToken);
        db.OtpChallenges.RemoveRange(existing);

        var challenge = new OtpChallenge
        {
            Phone = phone,
            CodeHash = codeHash,
            ExpiresAtUtc = expiresAtUtc,
        };

        await db.OtpChallenges.AddAsync(challenge, cancellationToken);
        return challenge;
    }

    /// <summary>
    /// Returns the challenge for this phone regardless of whether it has
    /// expired or been consumed — <see cref="OtpChallenge.Validate"/> is what
    /// decides that, so its caller gets the specific reason rather than a bare
    /// "not found".
    /// </summary>
    /// <remarks>
    /// <see cref="CreateAsync"/> always removes any prior challenge for the
    /// phone before adding the new one, so at most one row can exist here —
    /// deliberately <see cref="Queryable.SingleOrDefaultAsync{TSource}(IQueryable{TSource}, CancellationToken)"/>
    /// rather than an ordered <c>FirstOrDefault</c>, so a second row (that
    /// invariant broken somewhere) throws instead of silently picking one.
    /// This also sidesteps SQLite's test-provider limitation on ordering by
    /// <see cref="DateTimeOffset"/> — Postgres has no such limitation, but the
    /// simpler query is correct on both rather than special-cased for either.
    /// </remarks>
    public Task<OtpChallenge?> FindActiveAsync(string phone, CancellationToken cancellationToken) =>
        db.OtpChallenges.SingleOrDefaultAsync(c => c.Phone == phone, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
