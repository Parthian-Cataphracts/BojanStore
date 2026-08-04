using System.Globalization;
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
    /// <summary>
    /// The <c>BZ-000000</c> shape the order screens render.
    /// </summary>
    /// <remarks>
    /// Six digits is nine hundred thousand numbers, and the column is uniquely
    /// indexed — so by the birthday bound a shop is more likely than not to hit
    /// a collision somewhere around its eleven-hundredth order, and a collision
    /// is an insert that violates the index in the middle of the money path.
    /// The suffix widens the space enough that it is not a thing that happens:
    /// four base-36 characters multiply it by another 1.6 million, and the code
    /// stays short enough to read down a phone line.
    /// </remarks>
    public static string NewOrderNumber() =>
        $"BZ-{RandomNumberGenerator.GetInt32(100_000, 1_000_000)}-{Suffix(4)}";

    /// <summary>
    /// The sixteen-digit number printed on a customer invoice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Random rather than sequential for the same reason
    /// <see cref="NewOrderNumber"/> is, and more sharply so: a sequential
    /// invoice number is a running total of everything the shop has ever sold,
    /// readable by anyone who bought two things a month apart.
    /// </para>
    /// <para>
    /// Sixteen digits is 10^16 values, and the column is uniquely indexed, so
    /// the birthday bound puts the first expected collision somewhere past a
    /// hundred million invoices — far enough out that the index is the whole of
    /// the guarantee and no re-draw loop is needed. Phonix checked each
    /// candidate against every existing invoice before using it because its
    /// store kept orders as JSON documents with no index to lean on.
    /// </para>
    /// </remarks>
    public static string NewInvoiceNumber()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var value = BitConverter.ToUInt64(bytes) % 10_000_000_000_000_000UL;
        return value.ToString("D16", CultureInfo.InvariantCulture);
    }

    private const string SuffixAlphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>
    /// Random characters from an alphabet with no <c>I</c> or <c>O</c> in it.
    /// </summary>
    /// <remarks>
    /// Both are read back as digits over the phone, which is exactly how a
    /// shopper uses an order number — the tracking form matches on it exactly.
    /// </remarks>
    private static string Suffix(int length) =>
        string.Create(length, length, static (span, count) =>
        {
            for (var index = 0; index < count; index++)
            {
                span[index] = SuffixAlphabet[RandomNumberGenerator.GetInt32(SuffixAlphabet.Length)];
            }
        });

    /// <summary>The <c>RT-XX-000</c> shape screens 35 and 36 render.</summary>
    public static string NewReturnCode() => $"RT-BZ-{RandomNumberGenerator.GetInt32(100, 1_000)}";

    /// <summary>The <c>B2B-0000</c> shape the business screens render.</summary>
    public static string NewBusinessRequestCode() => $"B2B-{RandomNumberGenerator.GetInt32(1_000, 10_000)}";

    /// <summary>The <c>QT-0000-0000</c> shape screen 65 renders.</summary>
    public static string NewQuoteNumber() =>
        $"QT-{RandomNumberGenerator.GetInt32(1_000, 10_000)}-{RandomNumberGenerator.GetInt32(1_000, 10_000)}";
}
