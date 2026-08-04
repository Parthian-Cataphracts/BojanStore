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
    /// Returns the newest challenge for this phone regardless of whether it has
    /// expired or been consumed — <see cref="OtpChallenge.Validate"/> is what
    /// decides that, so its caller gets the specific reason rather than a bare
    /// "not found".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CreateAsync"/> clears the phone's prior challenges before
    /// adding one, so in the ordinary case there is exactly one row. That is a
    /// read followed by a write, though, not an atomic swap, and the index on
    /// <c>Phone</c> is not unique — two sign-in requests for the same number
    /// arriving together each delete what they saw and each insert their own,
    /// leaving two rows behind.
    /// </para>
    /// <para>
    /// This used to be <c>SingleOrDefaultAsync</c>, chosen so that a broken
    /// invariant would throw rather than silently pick a row. In this position
    /// that trade is the wrong way round: the throw lands on
    /// <c>POST /auth/otp/verify</c>, so a race while requesting a code turned
    /// every subsequent attempt to use one into a 500 — the shopper cannot sign
    /// in at all until they ask for another code, and nothing tells them that is
    /// the way out. Picking the newest is also exactly what the domain already
    /// says happens: a new request supersedes whatever was pending.
    /// </para>
    /// <para>
    /// The ordering is done after materialising rather than in SQL because
    /// <see cref="DateTimeOffset"/> is not orderable by SQLite, which
    /// <c>Bojan.Api.Tests</c> runs against. The set being ordered is one phone's
    /// challenges — one row outside the race it exists to survive.
    /// </para>
    /// </remarks>
    public async Task<OtpChallenge?> FindActiveAsync(string phone, CancellationToken cancellationToken)
    {
        var challenges = await db.OtpChallenges
            .Where(c => c.Phone == phone)
            .ToListAsync(cancellationToken);

        return challenges.Count <= 1
            ? challenges.SingleOrDefault()
            : challenges.MaxBy(c => c.ExpiresAtUtc);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
