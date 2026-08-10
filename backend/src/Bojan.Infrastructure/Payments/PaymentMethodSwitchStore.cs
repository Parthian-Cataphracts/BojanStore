using Bojan.Application.Contracts;
using Bojan.Application.Payments;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Payments;

/// <summary>
/// The three checkout payment options, on and off.
/// </summary>
/// <remarks>
/// <para>
/// Backed by the <c>PaymentMethod</c> rows the checkout validates against, so
/// the switch and the thing it switches are one value. The panel screen used to
/// write these into the settings table, where nothing read them: turning cash
/// on delivery off changed what the screen said and left the option on the
/// checkout.
/// </para>
/// <para>
/// A code the shop does not have a row for is left alone rather than created.
/// The rows carry a title, a note, an icon and a sort order that an operator
/// may have edited, and inventing one here would put unwritten defaults on the
/// checkout under a name nobody chose.
/// </para>
/// </remarks>
public sealed class PaymentMethodSwitchStore(BojanDbContext db) : IPaymentMethodSwitchStore
{
    /// <summary>The wire codes, matching <c>PaymentMethod.Code</c>.</summary>
    private const string Online = "gateway";

    private const string Wallet = "wallet";

    private const string CashOnDelivery = "cod";

    public async Task<PaymentMethodSwitchesDto> GetAsync(CancellationToken cancellationToken)
    {
        var active = await db.PaymentMethods.AsNoTracking()
            .ToDictionaryAsync(method => method.Code, method => method.IsActive, cancellationToken);

        return new PaymentMethodSwitchesDto(
            Online: active.GetValueOrDefault(Online),
            Wallet: active.GetValueOrDefault(Wallet),
            CashOnDelivery: active.GetValueOrDefault(CashOnDelivery));
    }

    public async Task SaveAsync(PaymentMethodSwitchesDto switches, CancellationToken cancellationToken)
    {
        var wanted = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            [Online] = switches.Online,
            [Wallet] = switches.Wallet,
            [CashOnDelivery] = switches.CashOnDelivery,
        };

        var methods = await db.PaymentMethods
            .Where(method => wanted.Keys.Contains(method.Code))
            .ToListAsync(cancellationToken);

        foreach (var method in methods)
        {
            method.IsActive = wanted[method.Code];
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
