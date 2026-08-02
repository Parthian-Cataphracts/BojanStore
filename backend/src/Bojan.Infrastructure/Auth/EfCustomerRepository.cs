using Bojan.Application.Auth;
using Bojan.Domain.Customers;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfCustomerRepository(BojanDbContext db) : ICustomerRepository
{
    public Task<Customer?> FindByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);

    /// <summary>
    /// Matched on the stored value directly.
    /// </summary>
    /// <remarks>
    /// The application layer lower-cases every address before it reaches this
    /// repository — on the way in as well as on the way out — so the comparison
    /// is ordinary equality and stays index-friendly. A <c>ToLower()</c> here
    /// would be a scan of the table on every sign-in.
    /// </remarks>
    public Task<Customer?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Email == email, cancellationToken);

    public Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> EmailTakenAsync(string email, Guid? exceptCustomerId, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(
            c => c.Email == email && (exceptCustomerId == null || c.Id != exceptCustomerId),
            cancellationToken);

    public async Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await db.Customers.AddAsync(customer, cancellationToken);
        return customer;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
