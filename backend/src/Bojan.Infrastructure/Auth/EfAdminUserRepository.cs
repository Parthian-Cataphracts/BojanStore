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

    public Task<AdminUser?> FindByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.AdminUsers
            .Include(u => u.Sections)
            .FirstOrDefaultAsync(u => u.CustomerId == customerId, cancellationToken);

    public Task<AdminUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.AdminUsers.Include(u => u.Sections).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
