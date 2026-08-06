namespace Bojan.Domain.Orders;

/// <summary>
/// Thrown when an order is asked to move further along fulfilment than an
/// unpaid order may go.
/// </summary>
/// <remarks>
/// Its own type rather than the <see cref="InvalidOperationException"/> the
/// other transition guards throw, because the caller has to tell the two apart:
/// "this order is already delivered" and "nobody has paid for this order" are
/// different problems with different fixes, and an operator handed one message
/// for both would go looking in the wrong place.
/// </remarks>
public sealed class OrderNotPaidException(string orderNumber, OrderStatus attempted)
    : InvalidOperationException(
        $"Order {orderNumber} has not been paid for and cannot move to {attempted}.")
{
    public string OrderNumber { get; } = orderNumber;

    public OrderStatus AttemptedStatus { get; } = attempted;
}
