using Bojan.Domain.Common;

namespace Bojan.Domain.Customers;

/// <summary>
/// A storefront shopper, identified by phone number.
/// </summary>
/// <remarks>
/// Mirrors the frontend's <c>User</c> DTO in
/// <c>apps/storefront/src/lib/api/types.ts</c>. There is no password —
/// sign-in is phone + SMS code only, matching <c>LoginForm.tsx</c> and
/// <c>BACKEND.md</c> section 1.3.
/// </remarks>
public sealed class Customer : Entity
{
    /// <summary>11-digit Iranian mobile number, e.g. <c>09121234567</c>. The unique sign-in key.</summary>
    public required string Phone { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>
    /// PBKDF2 hash of the customer's password, or null for an account that has
    /// only ever signed in with a one-time code.
    /// </summary>
    /// <remarks>
    /// Nullable on purpose, and permanently so. A phone number is still enough
    /// to sign in — registering with a password is the alternative for shoppers
    /// whose SMS does not arrive, not a replacement. Every account created
    /// before this existed has none, and forcing one on them at their next
    /// sign-in would lock out exactly the people the code path already works
    /// for.
    /// </remarks>
    public string? PasswordHash { get; set; }

    /// <summary>Stored as a plain date (no time component); the frontend renders it as Jalali.</summary>
    public DateOnly? BirthDate { get; set; }

    public string? City { get; set; }

    public string? NationalId { get; set; }

    public string? AvatarUrl { get; set; }

    public Money WalletBalance { get; private set; } = Money.Zero;

    public int LoyaltyPoints { get; private set; }

    /// <summary>
    /// Segment shown in the panel's customer list (<c>AdminCustomer.group</c>).
    /// Free text rather than an enum: screen 100 lets an operator define groups
    /// by rule, so the set is data, not code.
    /// </summary>
    public string Group { get; set; } = "عادی";

    /// <summary>A blocked customer keeps their history but cannot sign in — the panel's <c>AdminCustomer.status</c>.</summary>
    public bool IsBlocked { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    private readonly List<Address> _addresses = [];
    public IReadOnlyCollection<Address> Addresses => _addresses;

    /// <summary>Credits the wallet — a positive top-up or a refund. Throws on a negative amount; use <see cref="DebitWallet"/> to spend.</summary>
    public void CreditWallet(Money amount) => WalletBalance += amount;

    /// <summary>Spends from the wallet. Throws if the balance is insufficient — the caller must check first.</summary>
    public void DebitWallet(Money amount)
    {
        if (amount > WalletBalance)
        {
            throw new InvalidOperationException("Insufficient wallet balance.");
        }

        WalletBalance -= amount;
    }

    public void AddLoyaltyPoints(int points)
    {
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "Loyalty points cannot be negative.");
        }

        LoyaltyPoints += points;
    }

    public Address AddAddress(Address address)
    {
        if (address.IsDefault)
        {
            foreach (var existing in _addresses)
            {
                existing.IsDefault = false;
            }
        }

        _addresses.Add(address);
        return address;
    }
}
