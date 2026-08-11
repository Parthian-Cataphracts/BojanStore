using Bojan.Application.Accounts;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Orders;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;

namespace Bojan.Application.Payments;

/// <summary>
/// Settles what a shopper comes back from the gateway with.
/// </summary>
/// <remarks>
/// <para>
/// One entry point for both things a gateway return can be about. ZarinPal
/// hands the shopper back an authority and a status and nothing else, so which
/// of the two it was — an order or a wallet top-up — is a question only the
/// shop's own records can answer. Resolving it here rather than on the page is
/// also what keeps the browser out of the decision: the reference is matched
/// against rows this shop wrote, scoped to the customer who is signed in.
/// </para>
/// <para>
/// <b>Nothing here trusts the query string.</b> A shopper arriving with
/// <c>Status=OK</c> has proved only that they can type. The gateway is asked,
/// with the amount taken from the stored order rather than from the request,
/// and its answer is the only thing written.
/// </para>
/// <para>
/// <b>A refusal is not written as a failure.</b> A verification that comes back
/// negative is usually an abandoned payment, and
/// <see cref="Order.MarkPaymentFailed"/> is close to a one-way door —
/// <see cref="Order.MarkPaid"/> only acts on an order that is still awaiting
/// payment, so recording a failure would take the operator's manual settle path
/// away from an order that may yet be paid by transfer. The order is left
/// awaiting payment and the shopper is told the payment did not complete.
/// </para>
/// </remarks>
public sealed class PaymentSettlementService(
    IPaymentSettlementRepository repository,
    AccountService accounts,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    /// <summary>
    /// <c>POST /me/payments/callback</c> — where the gateway sends the shopper
    /// back to.
    /// </summary>
    /// <remarks>
    /// Safe to call repeatedly, which matters because a shopper who refreshes
    /// the callback page calls it again, and it has to stay safe when two of
    /// those arrive at once.
    /// </remarks>
    public async Task<UseCaseResult<PaymentCallbackResultDto>> SettleAsync(
        Guid customerId,
        string reference,
        CancellationToken cancellationToken)
    {
        reference = reference.Trim();
        if (reference.Length is 0 or > 200)
        {
            return UseCaseResult<PaymentCallbackResultDto>.Failure(UseCaseError.Invalid, "reference");
        }

        // Orders first, because they are the common case and because a top-up
        // reference and an order reference cannot collide: both are issued by
        // the gateway, and a gateway does not reuse an authority.
        var order = await repository.PeekByReferenceAsync(customerId, reference, cancellationToken);
        if (order is not null)
        {
            return await SettleOrderAsync(order, reference, cancellationToken);
        }

        var topUp = await accounts.ConfirmGatewayTopUpAsync(customerId, reference, cancellationToken);

        if (topUp.IsSuccess)
        {
            return new PaymentCallbackResultDto(PaymentCallbackResultDto.Wallet, null, reference, Paid: true);
        }

        // A declined top-up is reported as an unpaid wallet callback rather than
        // as an error, so the page can say which of the two it was and send the
        // shopper somewhere that makes sense. Anything else — an unknown
        // reference, someone else's — keeps its own failure.
        return topUp.Error is UseCaseError.Invalid
            ? new PaymentCallbackResultDto(PaymentCallbackResultDto.Wallet, null, reference, Paid: false)
            : UseCaseResult<PaymentCallbackResultDto>.Failure(topUp.Error!.Value, topUp.Detail);
    }

    /// <summary>
    /// Asks the gateway about one order and records the answer.
    /// </summary>
    /// <remarks>
    /// The gateway call stays outside the transaction on purpose: it is a
    /// network round trip and a read, and holding a row lock across someone
    /// else's HTTP timeout is how a lock queue becomes an outage. The status
    /// check that makes the write idempotent is made after it, under the lock.
    /// </remarks>
    private async Task<UseCaseResult<PaymentCallbackResultDto>> SettleOrderAsync(
        Order order,
        string reference,
        CancellationToken cancellationToken)
    {
        if (order.PaymentStatus is not OrderPaymentStatus.AwaitingPayment)
        {
            // Already settled — report what it settled as rather than failing,
            // so a refreshed callback page shows the outcome instead of an error.
            return new PaymentCallbackResultDto(
                PaymentCallbackResultDto.Order,
                order.Number,
                reference,
                order.PaymentStatus is OrderPaymentStatus.Paid);
        }

        // The amount comes from the stored order, never from the request. It is
        // the same figure the payment was started for, and ZarinPal answers -50
        // when a verification names a different one — which is exactly the check
        // that would be given away by letting the caller supply it.
        var verified = await gateway.VerifyAsync(
            reference,
            order.Number,
            order.PayableOnline.Amount,
            cancellationToken);

        if (!verified)
        {
            return new PaymentCallbackResultDto(PaymentCallbackResultDto.Order, order.Number, reference, Paid: false);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var locked = await repository.FindByReferenceForUpdateAsync(reference, token);
                if (locked is null)
                {
                    return UseCaseResult<PaymentCallbackResultDto>.Failure(UseCaseError.NotFound);
                }

                if (locked.MarkPaid(clock.UtcNow, reference, settledBy: null))
                {
                    repository.AddNotification(new CustomerNotification
                    {
                        CustomerId = locked.CustomerId,
                        Kind = NotificationKind.Order,
                        Title = $"سفارش {locked.Number}",
                        Body = OrderNotices.PaymentConfirmed(locked.Number),
                        CreatedAtUtc = clock.UtcNow,
                    }.WithLink($"/account/orders/{locked.Id}"));

                    await unitOfWork.SaveChangesAsync(token);
                }

                // Whether this call or the one racing it did the writing, the
                // order is paid and that is what the shopper is told.
                return new PaymentCallbackResultDto(
                    PaymentCallbackResultDto.Order,
                    locked.Number,
                    reference,
                    Paid: true);
            },
            cancellationToken);
    }

    /// <summary>
    /// Re-asks the gateway about one abandoned order — the reconciliation
    /// worker's unit of work.
    /// </summary>
    /// <remarks>
    /// A shopper who pays and then closes the tab never reaches the callback,
    /// and without this the shop has their money while their order reads "در
    /// انتظار پرداخت" until somebody notices. ZarinPal answers <c>101</c> for an
    /// authority that was already verified and <c>-51</c> for one that was never
    /// paid, so asking again is both safe and conclusive.
    /// </remarks>
    /// <returns>True when the order is paid — by this call or by one before it.</returns>
    public async Task<bool> ReconcileAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.PaymentReference is not { Length: > 0 } reference)
        {
            return false;
        }

        var settled = await SettleOrderAsync(order, reference, cancellationToken);
        return settled is { IsSuccess: true, Value.Paid: true };
    }
}
