using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfAdminUserRepository(BojanDbContext db) : IAdminUserRepository
{
    public Task<AdminUser?> FindByIdentityAsync(string identity, CancellationToken cancellationToken) =>
        db.AdminUsers.FirstOrDefaultAsync(
            u => u.Email.ToLower() == identity.ToLower() || u.Phone == identity,
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
