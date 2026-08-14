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

        // Through the same sequence `AddAsync` uses. This is the other path a
        // customer comes into existence by — a one-time code, or an operator
        // signing in on the storefront for the first time — and without it
        // those accounts kept the entity's unreadable fallback code while
        // registrations got `BZ-00042`.
        var created = new Customer { Phone = phone, Code = await NextCodeAsync(cancellationToken) };
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

    /// <summary>
    /// The one place a customer comes into existence, which is why the shop's
    /// own reference is issued here.
    /// </summary>
    /// <remarks>
    /// Every registration path — the password form, the one-time code, an
    /// operator creating an account — arrives through this method, so a code
    /// assigned here cannot be forgotten by a caller. It comes from a Postgres
    /// sequence rather than from counting the table: two registrations in the
    /// same instant would read the same count, build the same code, and one of
    /// them would fail against the unique index.
    /// </remarks>
    public async Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        if (customer.Code.Length == 0)
        {
            customer.Code = await NextCodeAsync(cancellationToken);
        }

        await db.Customers.AddAsync(customer, cancellationToken);
        return customer;
    }

    private async Task<string> NextCodeAsync(CancellationToken cancellationToken)
    {
        // SQLite has no sequences and the tests do not run on it any more, but a
        // provider check keeps this from being the reason a future one cannot.
        if (!db.Database.IsNpgsql())
        {
            return $"BZ-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }

        var next = await db.Database
            .SqlQuery<long>($"SELECT nextval('customer_code_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return $"BZ-{next:D5}";
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
