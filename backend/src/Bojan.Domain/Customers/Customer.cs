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

    /// <summary>
    /// The shop's own reference for this customer — <c>BZ-00042</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Short, unique, and safe to say out loud. The id is a GUID, which is the
    /// right key for a database and the wrong thing to read down a phone line
    /// or write on a parcel; the phone number is the alternative and it is the
    /// customer's personal data. So there is a third handle, and it exists
    /// because a shop needs one every time it talks about a customer without
    /// talking to them.
    /// </para>
    /// <para>
    /// Assigned on creation and editable afterwards, because a shop migrating
    /// from another system arrives with codes its staff already know.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Defaulted to something unique rather than to <c>""</c>. The readable
    /// running number comes from a database sequence in the repository, which
    /// is the one path every registration takes — but the column is unique, and
    /// a default that is the same for every instance turns any code that builds
    /// a <see cref="Customer"/> without going through that path into a
    /// unique-index violation on the second row. Tests and the seeder do
    /// exactly that. A distinct fallback means such a row saves; it simply gets
    /// a code nobody would read aloud.
    /// </remarks>
    public string Code { get; set; } = $"BZ-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>
    /// Whether <see cref="Email"/> has been confirmed to belong to this
    /// customer — a link they clicked, not merely a string they typed.
    /// </summary>
    /// <remarks>Reset to false whenever <see cref="Email"/> is changed to a genuinely new value — see <c>AccountService.UpdateProfileAsync</c>.</remarks>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// Whether <see cref="Phone"/> has been confirmed by a code sent to it.
    /// </summary>
    /// <remarks>
    /// Set on creation for an account born from the phone+OTP sign-in path —
    /// receiving that first code already proved the number. An account created
    /// through <c>CustomerPasswordService.RegisterAsync</c> starts false, since
    /// registering never sends anything to the phone. Reset to false whenever
    /// <see cref="Phone"/> changes to a genuinely new value.
    /// </remarks>
    public bool IsPhoneVerified { get; set; }

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

    /// <summary>
    /// Changes whenever every existing session should stop working.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A session here is a signed cookie carrying its own expiry, which is what
    /// makes it verifiable without asking the database — and also what made it
    /// impossible to end early. Changing a password did not sign anyone out, so
    /// the one action a person takes when they think someone else is in their
    /// account did not put them out of it.
    /// </para>
    /// <para>
    /// The stamp is the missing half: it travels with the session and is
    /// compared against this value, so rotating it makes every cookie issued
    /// before the rotation fail to authenticate while leaving the signing scheme
    /// exactly as it was.
    /// </para>
    /// </remarks>
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();

    /// <summary>Ends every session issued before now. Called wherever the password changes.</summary>
    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();

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
