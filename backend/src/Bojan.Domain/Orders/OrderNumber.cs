using System.Security.Cryptography;

namespace Bojan.Domain.Orders;

/// <summary>
/// Generates the human-facing codes the frontend renders.
/// </summary>
/// <remarks>
/// Every one of these is random rather than sequential, and that is the point:
/// a sequential order number tells anyone who has one roughly how many orders
/// the shop has taken, and makes <c>GET /orders/track</c> walkable. The public
/// tracking endpoint additionally matches on phone (<c>BACKEND.md</c> Phase 4),
/// but a number that cannot be guessed in the first place is the cheaper half
/// of that defence.
/// </remarks>
public static class OrderNumber
{
    /// <summary>The <c>BZ-0000</c>/<c>BJ-000000</c> shape the order screens render.</summary>
    public static string NewOrderNumber() => $"BZ-{RandomNumberGenerator.GetInt32(100_000, 1_000_000)}";

    /// <summary>The <c>RT-XX-000</c> shape screens 35 and 36 render.</summary>
    public static string NewReturnCode() => $"RT-BZ-{RandomNumberGenerator.GetInt32(100, 1_000)}";

    /// <summary>The <c>B2B-0000</c> shape the business screens render.</summary>
    public static string NewBusinessRequestCode() => $"B2B-{RandomNumberGenerator.GetInt32(1_000, 10_000)}";

    /// <summary>The <c>QT-0000-0000</c> shape screen 65 renders.</summary>
    public static string NewQuoteNumber() =>
        $"QT-{RandomNumberGenerator.GetInt32(1_000, 10_000)}-{RandomNumberGenerator.GetInt32(1_000, 10_000)}";
}
