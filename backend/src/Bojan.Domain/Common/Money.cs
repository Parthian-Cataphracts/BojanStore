namespace Bojan.Domain.Common;

/// <summary>
/// An amount of Iranian Toman.
/// </summary>
/// <remarks>
/// The frontend's <c>Money</c> type and every price field it renders is an
/// integer number of Toman — never Rial, never a fractional unit (see
/// <c>BACKEND.md</c>, section 1.2). Wrapping <see cref="long"/> in a value
/// object rather than passing raw longs around means "is this Rial or
/// Toman?" can never be asked at a call site — there is only one unit.
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    public long Amount { get; }

    public Money(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Money cannot be negative.");
        }

        Amount = amount;
    }

    public static readonly Money Zero = new(0);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right)
    {
        if (right.Amount > left.Amount)
        {
            throw new InvalidOperationException("Money cannot go negative — clamp with TryMinus if that is expected.");
        }

        return new Money(left.Amount - right.Amount);
    }

    /// <summary>Subtracts, clamping at zero instead of throwing — for discounts that must never exceed the goods.</summary>
    public Money ClampedMinus(Money other) => new(Math.Max(0, Amount - other.Amount));

    public static Money operator *(Money money, int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity cannot be negative.");
        }

        return new Money(money.Amount * quantity);
    }

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
