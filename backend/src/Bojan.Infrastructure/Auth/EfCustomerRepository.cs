using Bojan.Application.Auth;
using Bojan.Domain.Customers;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfCustomerRepository(BojanDbContext db) : ICustomerRepository
{
    public Task<Customer?> FindByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);

    public async Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await db.Customers.AddAsync(customer, cancellationToken);
        return customer;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
