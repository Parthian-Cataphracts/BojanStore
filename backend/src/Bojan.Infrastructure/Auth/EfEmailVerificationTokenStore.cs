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

    public Task<EmailVerificationToken?> FindActiveForCustomerAsync(
        Guid customerId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens.FirstOrDefaultAsync(
            t => t.CustomerId == customerId && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now,
            cancellationToken);
}
