using Bojan.Application.Administration;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Inventory;
using Bojan.Domain.Orders;

namespace Bojan.Application.Orders;

/// <summary>
/// Deciding a return — the operator's half of <c>POST /me/returns</c>.
/// </summary>
/// <remarks>
/// <para>
/// The customer's half has existed since Phase 5: they file a request, it is
/// checked against what the order actually contained, and it is stored. Nothing
/// could then move it. <see cref="ReturnRequest.TransitionTo"/> had no callers
/// at all, so every request ever filed sat at
/// <see cref="ReturnStatus.Submitted"/> for good, and the tracker on screen 36
/// drew a first step that never advanced.
/// </para>
/// <para>
/// Its own service rather than another method on
/// <see cref="AdminOperationsService"/>, for the reason
/// <see cref="OrderCancellationService"/> is its own: it moves money and stock
/// rather than a label, and the two of them are now the only places in the panel
/// that put money back into a wallet. All of it inside one transaction against a
/// locked row — the status is the only thing standing between a double-clicked
/// approve and a refund paid twice, which is the same sentence the cancellation
/// and the wallet top-up queue are both written around.
/// </para>
/// <para>
/// Nothing here takes an amount from the caller. What a return is worth is
/// <see cref="ReturnRefund"/>'s answer, derived from the order's own frozen line
/// prices — see that type for why a body that could name its own figure would be
/// a hole rather than a convenience.
/// </para>
/// </remarks>
public sealed class ReturnDecisionService(
    IAdminRepository repository,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IDateTimeProvider clock)
{
    /// <summary>
    /// Moves a return request on, doing whatever that step actually means.
    /// </summary>
    /// <param name="next">
    /// Where the operator is sending it. <see cref="ReturnStatus.Refunded"/> pays
    /// the money back, <see cref="ReturnStatus.Received"/> may put the goods back
    /// on the shelf, and the rest are review steps that only move the tracker.
    /// </param>
    /// <param name="restock">
    /// The operator's judgement at the warehouse step, ignored at every other.
    /// A parcel that has been out of the shop's hands may come back damaged,
    /// short, or not the thing that was sold, so nothing infers this — see
    /// <see cref="ReturnRequest.Restocked"/>.
    /// </param>
    public Task<UseCaseResult<ReturnDecisionDto>> DecideAsync(
        Guid returnId,
        Guid actorId,
        ReturnStatus next,
        string? note,
        bool restock,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var request = await repository.FindReturnRequestForUpdateAsync(returnId, token);
                if (request is null)
                {
                    return UseCaseResult<ReturnDecisionDto>.Failure(UseCaseError.NotFound);
                }

                if (request.IsClosed)
                {
                    // Reported rather than repeated, so a double-clicked button
                    // is harmless instead of paying a second refund.
                    return UseCaseResult<ReturnDecisionDto>.Failure(UseCaseError.Conflict, "already-closed");
                }

                // Locked as well, and for both of the things it is read for: the
                // line prices a refund is computed from, and the stock counts a
                // restock increments. Loaded even for the review steps that need
                // neither, because a return whose order has vanished is not a
                // request an operator should be able to approve.
                var order = await repository.FindOrderForUpdateAsync(request.OrderId, token);
                if (order is null)
                {
                    return UseCaseResult<ReturnDecisionDto>.Failure(UseCaseError.NotFound, "order");
                }

                var outcome = ReturnRefund.For(order, request.Items);

                if (next is ReturnStatus.Refunded && !outcome.Payable)
                {
                    // The order's money never arrived — a delivered cash-on-
                    // delivery order nobody reconciled is the realistic case.
                    // Paying out against it would take cash out of the till for
                    // a sale that never went in.
                    return UseCaseResult<ReturnDecisionDto>.Failure(UseCaseError.Conflict, "order-not-paid");
                }

                try
                {
                    if (next is ReturnStatus.Refunded)
                    {
                        repository.AddReturnTimelineEvent(
                            request.Refund(outcome.Refund, clock.UtcNow, actorId, note));
                    }
                    else
                    {
                        repository.AddReturnTimelineEvent(
                            request.TransitionTo(next, clock.UtcNow, actorId, note));
                    }
                }
                catch (InvalidOperationException)
                {
                    // Closed, or a move that is not forward. Either way the
                    // operator asked for something the domain forbids, which is
                    // not a server fault.
                    return UseCaseResult<ReturnDecisionDto>.Failure(UseCaseError.Conflict, "not-allowed");
                }

                if (next is ReturnStatus.Received && restock)
                {
                    await RestockAsync(request, order, actorId, token);
                    request.MarkRestocked();
                }

                var paidToWallet = next is ReturnStatus.Refunded && request.RefundsToWallet
                    ? await CreditWalletAsync(request, order, outcome.Refund, token)
                    : Money.Zero;

                if (next is ReturnStatus.Refunded)
                {
                    await SettleOrderIfFullyReturnedAsync(order, outcome.Refund, request.Id, token);
                }

                repository.AddCustomerNotification(new CustomerNotification
                {
                    CustomerId = request.CustomerId,
                    Kind = NotificationKind.Order,
                    Title = $"مرجوعی {request.Code}",
                    Body = OrderNotices.ReturnDecided(next, request.Code, paidToWallet.Amount),
                    CreatedAtUtc = clock.UtcNow,
                }.WithLink($"/account/returns/{request.Id}"));

                audit.Record($"return.{WireFormat.ReturnStatus(next)}", request.Code);
                await unitOfWork.SaveChangesAsync(token);

                return new ReturnDecisionDto(
                    request.Code,
                    WireFormat.ReturnStatus(request.Status),
                    paidToWallet.Amount,
                    // What is owed but could not be paid here: a card refund the
                    // shop has to make at the bank, because no adapter behind
                    // IPaymentGateway can reverse a charge. Named so the
                    // confirmation says so rather than leaving the operator to
                    // know the rule.
                    outcome.Refund.ClampedMinus(paidToWallet).Amount,
                    request.Restocked);
            },
            cancellationToken);

    /// <summary>
    /// Puts the refund into the wallet and writes the ledger row the customer's
    /// wallet screen shows.
    /// </summary>
    /// <remarks>
    /// The same shape as the cancellation's refund beside it. A missing customer
    /// is not treated as a failure for the same reason it is not there: the
    /// return has still been decided, and refusing the whole decision because the
    /// account was deleted would leave the request stuck open for good.
    /// </remarks>
    private async Task<Money> CreditWalletAsync(
        ReturnRequest request,
        Order order,
        Money refund,
        CancellationToken cancellationToken)
    {
        if (refund == Money.Zero)
        {
            return Money.Zero;
        }

        var customer = await repository.FindCustomerForUpdateAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Money.Zero;
        }

        customer.CreditWallet(refund);

        repository.AddWalletTransaction(new WalletTransaction
        {
            CustomerId = request.CustomerId,
            Title = $"بازگشت وجه مرجوعی {request.Code} (سفارش {order.Number})",
            Amount = refund.Amount,
            Status = WalletTransactionStatus.Success,
            Icon = "undo",
            CreatedAtUtc = clock.UtcNow,
        });

        return refund;
    }

    /// <summary>
    /// Puts the returned units back, and records why each one moved.
    /// </summary>
    /// <remarks>
    /// Only the units named on the request, not the whole order — half a delivery
    /// coming back is the ordinary case, and restocking the lines wholesale would
    /// invent stock for the half the customer kept. A line that sold a variant
    /// returns to that variant's count rather than the parent product's, for the
    /// reason <c>OrderCancellationService.RestockAsync</c> gives.
    /// </remarks>
    private async Task RestockAsync(
        ReturnRequest request,
        Order order,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        // Matched on the combination as well as the product. An order can hold
        // two lines of one product in different variants, and matching on the
        // product alone credited whichever line came first — so returning a red
        // one put a blue one back on the shelf, and a variant that is out of
        // stock started looking available.
        var lines = request.Items
            .Select(item => (
                Item: item,
                Line: order.Lines.FirstOrDefault(l => l.ProductId == item.ProductId && l.SkuId == item.SkuId)))
            .Where(pair => pair.Line is not null)
            .ToList();

        var products = await repository.LoadProductsForUpdateAsync(
            [.. lines.Select(pair => pair.Item.ProductId)], cancellationToken);

        var skus = await repository.LoadSkusForUpdateAsync(
            [.. lines.Where(pair => pair.Line!.SkuId.HasValue).Select(pair => pair.Line!.SkuId!.Value)],
            cancellationToken);

        foreach (var (item, line) in lines)
        {
            if (line!.SkuId is { } skuId)
            {
                skus.FirstOrDefault(sku => sku.Id == skuId)?.IncreaseStock(item.Quantity);
            }
            else
            {
                products.FirstOrDefault(product => product.Id == item.ProductId)?.IncreaseStock(item.Quantity);
            }

            repository.AddStockMovement(new StockMovement
            {
                ProductId = item.ProductId,
                Kind = StockMovementKind.In,
                Quantity = item.Quantity,
                Reason = $"مرجوعی {request.Code}",
                Reference = order.Number,
                ActorId = actorId,
                AtUtc = clock.UtcNow,
            });
        }
    }

    /// <summary>
    /// Marks the order refunded once everything it sold has come back.
    /// </summary>
    /// <remarks>
    /// <see cref="OrderPaymentStatus.Refunded"/> is a fact about the whole order,
    /// so a partial return must not set it: most of the order was kept and paid
    /// for, and saying otherwise would put a repayment in the books that never
    /// happened. The bar is the order's goods value — subtotal less discount,
    /// shipping excluded, since that is the most a return can ever give back —
    /// measured against every refund the order has had, not only this one.
    /// </remarks>
    private async Task SettleOrderIfFullyReturnedAsync(
        Order order,
        Money refund,
        Guid exceptReturnId,
        CancellationToken cancellationToken)
    {
        var goodsValue = order.Subtotal.ClampedMinus(order.Discount);
        if (goodsValue == Money.Zero)
        {
            return;
        }

        var alreadyRefunded = await repository.SumRefundedReturnsAsync(order.Id, exceptReturnId, cancellationToken);

        if (alreadyRefunded + refund >= goodsValue)
        {
            order.MarkRefunded();
        }
    }
}
