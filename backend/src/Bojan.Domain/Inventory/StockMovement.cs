using Bojan.Domain.Common;

namespace Bojan.Domain.Inventory;

/// <summary>The frontend's <c>StockMovement.kind</c> — <c>'in' | 'out' | 'adjust'</c>.</summary>
public enum StockMovementKind
{
    In,
    Out,
    Adjust,
}

/// <summary>
/// One change to a product's stock, and why.
/// </summary>
/// <remarks>
/// Backs the panel's <c>inventory/movements</c> write (screens 108-111). The
/// movement row is the audit trail; <c>Product.Stock</c> is the running total
/// it produces. Both are written in the same transaction, so a movement that
/// did not move stock cannot exist and stock cannot move without a reason
/// recorded.
///
/// <see cref="Quantity"/> is always positive — <see cref="Kind"/> carries the
/// direction. An <see cref="StockMovementKind.Adjust"/> sets the count to
/// <see cref="Quantity"/> outright rather than adding to it, which is what the
/// panel's stocktake screen means by "adjust".
/// </remarks>
public sealed class StockMovement : Entity
{
    public required Guid ProductId { get; init; }

    public required StockMovementKind Kind { get; init; }

    public required int Quantity { get; init; }

    /// <summary>Why — a purchase order, a stocktake correction, damage.</summary>
    public required string Reason { get; init; }

    /// <summary>External document number, if the movement came with one.</summary>
    public string? Reference { get; init; }

    /// <summary>Operator who recorded it — the panel's list shows this as "by".</summary>
    public required Guid ActorId { get; init; }

    public DateTimeOffset AtUtc { get; init; } = DateTimeOffset.UtcNow;
}
