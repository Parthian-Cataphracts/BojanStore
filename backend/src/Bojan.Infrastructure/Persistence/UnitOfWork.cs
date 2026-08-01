using Bojan.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnitOfWork"/> over <see cref="BojanDbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// The transaction uses EF's execution strategy, which is what makes it safe
/// under Npgsql's retry-on-transient-failure: a strategy that retries a
/// user-initiated transaction has to be able to replay the whole block, and
/// <see cref="Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy.ExecuteAsync{TState,TResult}"/>
/// is how EF is told where that block starts.
/// </para>
/// <para>
/// A nested call joins the transaction already open rather than starting a
/// second — <see cref="CheckoutService"/> saves inside its own transaction, and
/// a second <c>BeginTransaction</c> underneath it would be an error, not a
/// savepoint.
/// </para>
/// </remarks>
public sealed class UnitOfWork(BojanDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);

            var result = await operation(token);

            await transaction.CommitAsync(token);
            return result;
        }, cancellationToken);
    }
}
