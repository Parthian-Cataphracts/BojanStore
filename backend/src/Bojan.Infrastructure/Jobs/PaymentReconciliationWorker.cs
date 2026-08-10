using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Jobs;

/// <summary>
/// Finds orders that were paid for while nobody was looking.
/// </summary>
/// <remarks>
/// <para>
/// The callback is not a delivery guarantee. A shopper who enters a card, sees
/// the bank's confirmation and then closes the tab — or loses signal on the way
/// back, or is bounced by a browser that blocks the redirect — never reaches
/// the page that would have asked the gateway. The money is gone from their
/// account and the order still reads "در انتظار پرداخت". Nothing else in the
/// system would ever look at it again.
/// </para>
/// <para>
/// So the shop asks. Every unsettled order that was sent to a gateway and is
/// old enough to have finished one way or the other gets re-verified, and
/// ZarinPal's answer is conclusive in both directions: <c>101</c> for an
/// authority that was already verified — which is what a paid-then-abandoned
/// payment looks like — and <c>-51</c> for one that was never paid.
/// </para>
/// <para>
/// The window has both ends. The near end keeps this from racing the shopper's
/// own callback over the same authority; the far end stops it from re-asking
/// about months of abandoned baskets on every cycle, which is a bill from the
/// gateway and a queue that never empties. An order older than the far end is
/// an operator's job, not a poll's.
/// </para>
/// <para>
/// It only runs while a provider that takes real money is configured. Against
/// the stub — which approves everything — it would mark every abandoned basket
/// in the window paid.
/// </para>
/// </remarks>
public sealed class PaymentReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentReconciliationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    /// <summary>Long enough that the shopper's own callback has had its chance.</summary>
    private static readonly TimeSpan SettleWindow = TimeSpan.FromMinutes(15);

    /// <summary>Past this, an unpaid order stops being a poll's problem.</summary>
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(2);

    /// <summary>
    /// Orders per cycle.
    /// </summary>
    /// <remarks>
    /// Each one is a round trip to the gateway, so this is a rate limit as much
    /// as a batch size — a backlog drains over several cycles rather than
    /// arriving at ZarinPal as one burst, which is what error -12 is for.
    /// </remarks>
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One order the gateway will not answer about must not stop
                // every later one from being asked.
                logger.LogError(exception, "The payment reconciliation cycle failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var gateway = provider.GetRequiredService<IPaymentGateway>();
        if (!await gateway.TakesRealMoneyAsync(cancellationToken))
        {
            return;
        }

        var clock = provider.GetRequiredService<IDateTimeProvider>();
        var repository = provider.GetRequiredService<IPaymentSettlementRepository>();
        var settlement = provider.GetRequiredService<PaymentSettlementService>();

        var now = clock.UtcNow;

        var candidates = await repository.ListUnsettledAsync(
            placedBeforeUtc: now - SettleWindow,
            placedAfterUtc: now - GiveUpAfter,
            limit: BatchSize,
            cancellationToken);

        foreach (var order in candidates)
        {
            try
            {
                if (await settlement.ReconcileAsync(order, cancellationToken))
                {
                    logger.LogWarning(
                        "Order {OrderNumber} was paid at the gateway but never confirmed by the shopper; settled by reconciliation.",
                        order.Number);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not reconcile order {OrderNumber}.", order.Number);
            }
        }
    }
}
