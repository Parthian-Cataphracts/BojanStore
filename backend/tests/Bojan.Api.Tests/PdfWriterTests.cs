using Bojan.Application.Contracts;
using Bojan.Infrastructure.Jobs;

namespace Bojan.Api.Tests;

/// <summary>
/// The format the export queue used to refuse.
/// </summary>
/// <remarks>
/// A PDF is hard to assert much about without rendering it, so these check the
/// two things the refusal actually rested on: that a file comes out at all, and
/// that the Persian font went into it. A PDF whose font is missing is exactly
/// the unreadable page — substituted glyphs, disconnected letters — that made
/// refusing the format better than producing one.
/// </remarks>
public sealed class PdfWriterTests
{
    private static readonly SalesDetailRow[] Rows =
    [
        new("BZ-1001", "1404/05/23 14:30", "مهدی شفیعی", "09120000000", "دفتر یادداشت A5", "SKU-1",
            2, 150_000, 300_000, "تحویل شده", "پرداخت شده", "پست پیشتاز", "پرداخت اینترنتی"),
        new("BZ-1002", "1404/05/24 09:05", "زهرا احمدی", "09121111111", "خودکار ژله‌ای", "SKU-2",
            5, 40_000, 200_000, "در حال آماده‌سازی", "در انتظار پرداخت", "پیک", "پرداخت در محل"),
    ];

    private static readonly DateTimeOffset From = new(1446, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(1446, 2, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_report_renders_to_a_real_pdf()
    {
        var bytes = PdfWriter.Write(Rows, "گزارش فروش", From, To);

        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void The_persian_font_travels_inside_the_file()
    {
        var bytes = PdfWriter.Write(Rows, "گزارش فروش", From, To);

        // The family name appears in the font descriptor once the glyphs are
        // embedded. Without it a reader substitutes whatever it has to hand.
        Assert.Contains("Vazirmatn", System.Text.Encoding.Latin1.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_range_still_produces_a_file()
    {
        // A month with no sales is an ordinary answer, not an error: the
        // operator gets the headings and no rows, rather than a failed job.
        var bytes = PdfWriter.Write(Array.Empty<SalesDetailRow>(), "گزارش فروش", From, To);

        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void Every_report_shape_renders()
    {
        // One row of each, because the writer reflects over the row type and a
        // shape it cannot read throws rather than producing a wrong page.
        Assert.NotEmpty(PdfWriter.Write(
            [new OrdersDetailRow("BZ-1", "1404/05/23", "مهدی", "0912", 2, 100, 10, 5, 95, "تحویل شده", "پرداخت شده", "", "")],
            "گزارش سفارش‌ها", From, To));

        Assert.NotEmpty(PdfWriter.Write(
            [new CustomersDetailRow("BZ-00001", "مهدی", "0912", "a@b.c", "تهران", "عادی", "فعال", 3, 900_000, "1404/05/20", "1404/01/01")],
            "گزارش مشتریان", From, To));

        Assert.NotEmpty(PdfWriter.Write(
            [new InventoryDetailRow("SKU-1", "دفتر", "نوشت‌افزار", "بوژان", 150_000, 4, 5, "کم‌موجود")],
            "گزارش موجودی انبار", From, To));

        Assert.NotEmpty(PdfWriter.Write(
            [new FinancialDetailRow("BZ-1", "1404/05/23", "مهدی", 95, 20, 75, "اینترنتی", "پرداخت شده", "ref", "1404/05/23")],
            "گزارش مالی", From, To));

        Assert.NotEmpty(PdfWriter.Write(
            [new CampaignsDetailRow("عنوان", "sms", "همه مشتریان", "1404/05/01", "1404/05/02", 10, 9, 1)],
            "گزارش کمپین‌ها", From, To));
    }
}
