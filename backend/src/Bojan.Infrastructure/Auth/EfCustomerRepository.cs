using Bojan.Application.Auth;
using Bojan.Domain.Customers;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

public sealed class EfCustomerRepository(BojanDbContext db) : ICustomerRepository
{
    public Task<Customer?> FindByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);

    public async Task<(Customer Customer, bool Created)> GetOrCreateByPhoneAsync(
        string phone,
        CancellationToken cancellationToken)
    {
        if (await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken) is { } existing)
        {
            return (existing, false);
        }

        var created = new Customer { Phone = phone };
        db.Customers.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return (created, true);
        }
        catch (DbUpdateException)
        {
            // Another request got there first between the read above and this
            // insert, and the unique index on Phone refused ours. The account
            // the caller asked for now exists and is theirs, so this is not a
            // conflict to report — it is a row to read back.
            //
            // Detached first: the rejected entity is still in the change
            // tracker, and a tracked query would resolve to it rather than
            // going to the database, handing back the row that was never saved.
            db.Entry(created).State = EntityState.Detached;

            var winner = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);
            if (winner is null) throw;

            return (winner, false);
        }
    }

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
