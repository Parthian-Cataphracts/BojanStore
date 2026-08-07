using System.Globalization;
using Bojan.Application.Administration;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
using Bojan.Application.Contracts;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Inventory;
using Bojan.Domain.Orders;

namespace Bojan.Application.Orders;

/// <summary>
/// Cancelling an order — the only implementation of it.
/// </summary>
/// <remarks>
/// <para>
/// Two doors lead here: an operator on screen 95, and a customer on their own
/// order. They differ in who is allowed to do it and in whether the penalty
/// applies, and in nothing else — so they are two callers of one method rather
/// than two methods. Phonix draws the same line for the same reason: two paths
/// that both move money must not be two implementations of moving money.
/// </para>
/// <para>
/// What cancelling costs and gives back is decided by
/// <see cref="OrderCancellation"/> from how far the order got; this service is
/// the part that writes it down. All of it inside one transaction, against a
/// locked order row — the status is the only thing standing between a
/// double-clicked cancel and a refund paid twice.
/// </para>
/// </remarks>
public sealed class OrderCancellationService(
    IAdminRepository repository,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    ICustomerMailer mailer,
    EmailTemplates templates,
    IDateTimeProvider clock)
{
    /// <summary>
    /// The settings row holding the penalty, as a percentage.
    /// </summary>
    /// <remarks>
    /// A setting rather than a constant because it is a commercial decision the
    /// shop changes without a deploy — the same reason Phonix keeps it in
    /// <c>PricingSettings</c>. Absent or unparseable means zero: a shop that has
    /// never opened the screen does not silently start charging people.
    /// </remarks>
    public const string PenaltySection = "orders";
    public const string PenaltyKey = "cancellationPenaltyPercent";

    /// <summary>
    /// Cancels an order: puts the goods back, refunds the wallet less any
    /// penalty, and records all of it together.
    /// </summary>
    /// <param name="actorId">
    /// Whoever caused it — the operator, or the customer on their own order.
    /// Named on the audit entry and on each stock movement, so a count that
    /// jumps can always be traced to a decision.
    /// </param>
    /// <param name="requireCustomerId">
    /// Set on the customer's own path. The order must belong to them, and a
    /// stranger's order answers not-found rather than forbidden — an id that
    /// exists must not be distinguishable from one that does not.
    /// </param>
    /// <param name="chargePenalty">
    /// False when the shop cancelled rather than the customer. Charging someone
    /// for a decision that was not theirs is not a penalty, it is a deduction.
    /// </param>
    public Task<UseCaseResult<OrderCancellationDto>> CancelAsync(
        Guid orderId,
        Guid actorId,
        Guid? requireCustomerId,
        string? reason,
        bool chargePenalty,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var order = await repository.FindOrderForUpdateAsync(orderId, token);
                if (order is null || (requireCustomerId is { } owner && order.CustomerId != owner))
                {
                    return UseCaseResult<OrderCancellationDto>.Failure(UseCaseError.NotFound);
                }

                if (!OrderCancellation.CanCancel(order.Status))
                {
                    // Already cancelled, already returned, or delivered — a
                    // delivered order is returned rather than cancelled, and
                    // that flow has an inspection step this one has not.
                    return UseCaseResult<OrderCancellationDto>.Failure(UseCaseError.Conflict, "not-cancellable");
                }

                var outcome = OrderCancellation.For(
                    order.Status,
                    order.WalletPaid,
                    order.PayableOnline,
                    await ReadPenaltyPercentAsync(token),
                    chargePenalty,
                    order.PaymentStatus);

                if (outcome.Restocks)
                {
                    await RestockAsync(order, actorId, token);
                }

                if (outcome.Refund > Money.Zero)
                {
                    var customer = await repository.FindCustomerForUpdateAsync(order.CustomerId, token);
                    if (customer is not null)
                    {
                        customer.CreditWallet(outcome.Refund);

                        // The ledger row is what the customer's wallet screen
                        // shows, and what makes the penalty visible rather than
                        // a number they have to work out from the difference.
                        repository.AddWalletTransaction(new WalletTransaction
                        {
                            CustomerId = order.CustomerId,
                            Title = outcome.Penalty > Money.Zero
                                ? $"بازگشت وجه سفارش {order.Number} (پس از کسر جریمه لغو)"
                                : $"بازگشت وجه سفارش {order.Number}",
                            Amount = outcome.Refund.Amount,
                            Status = WalletTransactionStatus.Success,
                            Icon = "undo",
                            CreatedAtUtc = clock.UtcNow,
                        });
                    }
                }

                // The code goes back into circulation. The per-customer limit
                // released itself already — it is counted from orders and
                // cancelled ones are excluded — but the shop-wide counter only
                // ever went up, so a campaign of a hundred codes lost one for
                // good on every cancellation, including the ones the shop made.
                if (order.CouponCode is { Length: > 0 } couponCode)
                {
                    var coupon = await repository.FindCouponByCodeAsync(couponCode, token);
                    coupon?.ReleaseRedemption();
                }

                // Money that was collected and is going back is Refunded;
                // an order nobody paid for stays as it was. Marking an unpaid
                // order Refunded would put a repayment in the books that never
                // happened.
                if (outcome.Refund > Money.Zero || order.PaymentStatus is OrderPaymentStatus.Paid)
                {
                    order.MarkRefunded();
                }

                repository.AddOrderTimelineEvent(
                    order.TransitionTo(OrderStatus.Cancelled, actorId: actorId, reason: reason?.Trim()));

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    order.Note = reason.Trim();
                }

                repository.AddCustomerNotification(new CustomerNotification
                {
                    CustomerId = order.CustomerId,
                    Kind = NotificationKind.Order,
                    Title = $"سفارش {order.Number}",
                    Body = Message(order.Number, outcome),
                    CreatedAtUtc = clock.UtcNow,
                }.WithLink($"/account/orders/{order.Id}"));

                audit.Record(
                    requireCustomerId is null ? "order.cancelled" : "order.cancelled.by-customer",
                    order.Number);

                await unitOfWork.SaveChangesAsync(token);

                // Money moved, so the customer gets it in writing. The penalty
                // is explained only when one was charged, and the explanation
                // holds only because AppliesPenalty is what decided it — from
                // the warehouse onwards.
                var buyer = await repository.FindCustomerAsync(order.CustomerId, token);
                await mailer.SendAsync(
                    buyer?.Email,
                    templates.OrderCancelled(
                        order.Number,
                        order.Total.Amount,
                        outcome.Refund.Amount,
                        outcome.Penalty.Amount,
                        outcome.ManualGatewayRefund.Amount,
                        penaltyExplained: true),
                    token);

                return new OrderCancellationDto(
                    order.Number,
                    outcome.Refund.Amount,
                    outcome.Penalty.Amount,
                    outcome.Restocks,
                    outcome.ManualGatewayRefund.Amount);
            },
            cancellationToken);

    /// <summary>Puts every line back, and records why each one moved.</summary>
    /// <remarks>
    /// A line that sold a variant returns to that variant's count, not to the
    /// parent product's — they are separate numbers, and crediting the wrong one
    /// makes a variant that is out of stock look available. The movement rows
    /// are written beside the counts so the inventory screen explains the jump
    /// rather than showing an unattributed increase.
    /// </remarks>
    private async Task RestockAsync(Order order, Guid actorId, CancellationToken cancellationToken)
    {
        var products = await repository.LoadProductsForUpdateAsync(
            [.. order.Lines.Select(line => line.ProductId)], cancellationToken);

        var skus = await repository.LoadSkusForUpdateAsync(
            [.. order.Lines.Where(line => line.SkuId.HasValue).Select(line => line.SkuId!.Value)],
            cancellationToken);

        foreach (var line in order.Lines)
        {
            if (line.SkuId is { } skuId)
            {
                skus.FirstOrDefault(sku => sku.Id == skuId)?.IncreaseStock(line.Quantity);
            }
            else
            {
                products.FirstOrDefault(product => product.Id == line.ProductId)?.IncreaseStock(line.Quantity);
            }

            repository.AddStockMovement(new StockMovement
            {
                ProductId = line.ProductId,
                Kind = StockMovementKind.In,
                Quantity = line.Quantity,
                Reason = $"لغو سفارش {order.Number}",
                Reference = order.Number,
                ActorId = actorId,
                AtUtc = clock.UtcNow,
            });
        }
    }

    /// <summary>Zero when unset or unparseable — see <see cref="PenaltyKey"/>.</summary>
    private async Task<decimal> ReadPenaltyPercentAsync(CancellationToken cancellationToken)
    {
        var setting = await repository.FindSettingAsync(PenaltySection, PenaltyKey, cancellationToken);

        return setting is not null
            && decimal.TryParse(setting.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent)
            && percent > 0
                ? Math.Min(percent, 100m)
                : 0m;
    }

    private static string Message(string number, OrderCancellation.Outcome outcome)
    {
        if (outcome.Refund == Money.Zero)
        {
            return $"سفارش {number} لغو شد.";
        }

        var refunded = $"سفارش {number} لغو شد و {outcome.Refund.Amount:N0} تومان به کیف پول شما بازگشت.";

        return outcome.Penalty > Money.Zero
            ? $"{refunded} مبلغ {outcome.Penalty.Amount:N0} تومان بابت جریمه لغو کسر شد."
            : refunded;
    }
}
