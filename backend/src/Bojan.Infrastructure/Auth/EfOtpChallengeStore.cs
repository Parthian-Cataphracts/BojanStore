using Bojan.Application.Auth;
using Bojan.Domain.Identity;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfOtpChallengeStore(BojanDbContext db) : IOtpChallengeStore
{
    /// <summary>
    /// Replaces whatever was pending for this number with a new challenge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One statement, against a unique index on the phone. It used to be a
    /// delete of everything found followed by an insert — a read then a write,
    /// so two sign-in requests for the same number arriving together each
    /// deleted what they had seen and each inserted, and the number was left
    /// with two live challenges. <c>FindActiveAsync</c> took the newest to
    /// survive that, which worked and left the losing row in the table for
    /// good.
    /// </para>
    /// <para>
    /// An upsert makes the race resolve in the database instead: whichever
    /// request arrives second overwrites the first's row rather than adding to
    /// it, and the shopper gets the code from whichever request they were
    /// actually waiting on — the newest, which is what the domain already says
    /// happens.
    /// </para>
    /// <para>
    /// It saves rather than leaving that to the caller, because a raw upsert has
    /// already written by the time it returns and a change tracker that thinks
    /// otherwise would insert a second row on the next save. The one caller does
    /// nothing else in its unit of work — see <c>AuthService.RequestOtpAsync</c>.
    /// </para>
    /// </remarks>
    public async Task<OtpChallenge> CreateAsync(
        string phone,
        string codeHash,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var challenge = new OtpChallenge
        {
            Phone = phone,
            CodeHash = codeHash,
            ExpiresAtUtc = expiresAtUtc,
        };

        if (!db.Database.IsNpgsql())
        {
            // No `ON CONFLICT` to rely on. The old shape, kept so a provider
            // without it still works — the unique index turns the race into a
            // refusal here rather than into a duplicate.
            var existing = await db.OtpChallenges.Where(c => c.Phone == phone).ToListAsync(cancellationToken);
            db.OtpChallenges.RemoveRange(existing);
            await db.OtpChallenges.AddAsync(challenge, cancellationToken);
            return challenge;
        }

        // Attempts and Consumed are reset by the same statement: this is a new
        // challenge, and inheriting the failed attempts of the one it replaces
        // would mean asking for a fresh code did not give the shopper their
        // five tries back.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO otp_challenges ("Id", "Phone", "CodeHash", "Attempts", "ExpiresAtUtc", "Consumed")
            VALUES ({challenge.Id}, {phone}, {codeHash}, 0, {expiresAtUtc}, false)
            ON CONFLICT ("Phone") DO UPDATE SET
                "Id" = EXCLUDED."Id",
                "CodeHash" = EXCLUDED."CodeHash",
                "Attempts" = 0,
                "ExpiresAtUtc" = EXCLUDED."ExpiresAtUtc",
                "Consumed" = false
            """,
            cancellationToken);

        return challenge;
    }

    /// <summary>
    /// The challenge for this phone, expired or consumed or not —
    /// <see cref="OtpChallenge.Validate"/> is what decides that, so its caller
    /// gets the specific reason rather than a bare "not found".
    /// </summary>
    /// <remarks>
    /// One row per phone now that the index is unique and
    /// <see cref="CreateAsync"/> upserts, so this is a single lookup rather than
    /// the "take the newest of however many the race left" it had to be.
    /// </remarks>
    public Task<OtpChallenge?> FindActiveAsync(string phone, CancellationToken cancellationToken) =>
        db.OtpChallenges.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
