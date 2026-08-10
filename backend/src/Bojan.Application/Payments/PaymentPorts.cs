using Bojan.Application.Contracts;
using Bojan.Domain.Orders;

namespace Bojan.Application.Payments;

/// <summary>
/// Reads and writes which gateway the shop is pointed at.
/// </summary>
/// <remarks>
/// The settings live in the database rather than in configuration because the
/// owner sets them from the panel, on a running shop, without a deploy — the
/// same arrangement the support mailbox already uses for its account. That is
/// the whole reason this port exists: configuration is read once at startup,
/// and a merchant id entered at nine o'clock has to work at nine o'clock.
/// </remarks>
public interface IPaymentGatewaySettingsStore
{
    /// <summary>What the panel may see — never the merchant id.</summary>
    Task<PaymentGatewaySettingsDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="merchantId">
    /// Null leaves the stored one alone; an explicit empty string clears it.
    /// </param>
    Task SaveAsync(PaymentGatewaySettingsDto settings, string? merchantId, CancellationToken cancellationToken);
}

/// <summary>
/// Turns the three checkout payment options on and off.
/// </summary>
/// <remarks>
/// Backed by the <c>PaymentMethod</c> rows the checkout already validates
/// against, so the switch and the thing it claims to switch are one value. The
/// panel screen previously wrote these into the settings table, which nothing
/// read.
/// </remarks>
public interface IPaymentMethodSwitchStore
{
    Task<PaymentMethodSwitchesDto> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(PaymentMethodSwitchesDto switches, CancellationToken cancellationToken);
}

/// <summary>
/// Asks the configured provider whether this shop's credentials actually work.
/// </summary>
/// <remarks>
/// Separate from <see cref="Common.IPaymentGateway"/> because it is a
/// diagnostic, not part of the money path: it is the settings screen's test
/// button, and the answer it needs is a sentence for a person rather than a
/// boolean for a use case.
/// </remarks>
public interface IPaymentGatewayProbe
{
    Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The orders and top-ups a gateway callback can be about.
/// </summary>
/// <remarks>
/// Resolution is by gateway reference, not by anything the browser carries.
/// ZarinPal returns the shopper with an <c>Authority</c> and nothing else, and
/// that is the safer shape anyway: a reference the shop issued and stored is
/// evidence, an order number in a query string is a guess anyone can make.
/// </remarks>
public interface IPaymentSettlementRepository
{
    /// <summary>Records the session the shopper was sent to, against the order.</summary>
    Task SetPaymentSessionAsync(Guid orderId, string paymentUrl, string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Files the in-app notice that the money arrived.
    /// </summary>
    /// <remarks>
    /// Written inside the same transaction as the settlement, so an order that
    /// reads paid and a customer who was never told cannot come apart.
    /// </remarks>
    void AddNotification(Domain.Customers.CustomerNotification notification);

    /// <summary>An untracked look-up, to decide whether the reference is worth asking the gateway about.</summary>
    Task<Order?> PeekByReferenceAsync(Guid customerId, string reference, CancellationToken cancellationToken);

    /// <summary>
    /// The order behind a reference, with its row locked.
    /// </summary>
    /// <remarks>
    /// The lock is what makes <see cref="Order.MarkPaid"/>'s idempotence worth
    /// anything: without it two callbacks arriving together both read
    /// <c>AwaitingPayment</c>, and the second settles an order the first has
    /// already settled.
    /// </remarks>
    Task<Order?> FindByReferenceForUpdateAsync(string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Orders that were sent to a gateway, never came back, and are old enough
    /// to be worth asking about.
    /// </summary>
    /// <remarks>
    /// The reconciliation worker's queue. A shopper who pays and then closes
    /// the tab never triggers a callback, and without this the shop has their
    /// money and their order still reads "در انتظار پرداخت" forever.
    /// </remarks>
    Task<IReadOnlyList<Order>> ListUnsettledAsync(
        DateTimeOffset placedBeforeUtc,
        DateTimeOffset placedAfterUtc,
        int limit,
        CancellationToken cancellationToken);
}
