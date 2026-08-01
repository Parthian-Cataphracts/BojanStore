using System.Linq.Expressions;
using Bojan.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bojan.Infrastructure.Persistence;

/// <summary>
/// Stores an optional <see cref="Money"/> as a nullable <c>bigint</c> column —
/// an integer count of Toman, per <c>BACKEND.md</c> section 1.2. There is
/// deliberately no separate currency column: the whole system has exactly one
/// unit.
/// </summary>
/// <remarks>
/// Only <em>optional</em> money uses this. A required <see cref="Money"/> is
/// mapped as a complex property instead (see <see cref="MoneyMapping"/>), which
/// costs the same single column but keeps <c>Amount</c> visible to LINQ.
/// </remarks>
public sealed class MoneyValueConverter() : ValueConverter<Money, long>(
    money => money.Amount,
    amount => new Money(amount));

/// <summary>
/// Maps a required <see cref="Money"/> to one <c>bigint</c> column.
/// </summary>
/// <remarks>
/// A complex property rather than a value conversion, because the aggregates on
/// the panel's dashboard and reports have to sum and compare these in SQL
/// (<c>BACKEND.md</c> Phase 6: "Push these into SQL — do not fetch rows and sum
/// them in C#"). A value-converted property is opaque to LINQ —
/// <c>o.Subtotal.Amount</c> has no translation — while a complex property's
/// <c>Amount</c> is an ordinary mapped column that <c>SUM</c>, <c>WHERE</c> and
/// <c>ORDER BY</c> all reach.
///
/// The column keeps the property's own name (<c>Price</c>, not
/// <c>Price_Amount</c>), so the schema reads exactly as it would have with a
/// converter.
/// </remarks>
public static class MoneyMapping
{
    public static EntityTypeBuilder<TEntity> MapMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Money>> property,
        string columnName)
        where TEntity : class
    {
        builder.ComplexProperty(property, money => money.Property(m => m.Amount).HasColumnName(columnName));
        return builder;
    }
}
