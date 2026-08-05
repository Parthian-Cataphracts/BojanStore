using System.Net;
using System.Text;

namespace Bojan.Application.Notifications;

/// <summary>A message ready to send: the text body and its formatted alternative.</summary>
public sealed record EmailBody(string Text, string Html);

/// <summary>
/// The shared frame every transactional email is built in.
/// </summary>
/// <remarks>
/// <para>
/// Tables and inline styles, not the CSS the rest of this product is written
/// in. Gmail strips <c>&lt;style&gt;</c> blocks in several of its clients and
/// neither it nor Outlook can be relied on for flex or grid — so the layout
/// that works everywhere is the one email has always used.
/// </para>
/// <para>
/// The palette is the shop's own: deep teal for the masthead, coral for the one
/// action, warm paper behind. Taken from <c>@bojan/config</c>'s tokens rather
/// than picked, so an email looks like it came from the same place as the site.
/// </para>
/// <para>
/// No images. Not a logo, not a product photo, not a spacer. Clients block
/// remote images by default, so an email built on them arrives broken — and a
/// remote image is also a read receipt, which is not a thing to do to a
/// customer over a delivery notice.
/// </para>
/// </remarks>
public static class EmailShell
{
    private const string Brand = "بوژان";
    private const string BrandLatin = "BOJAN STORE";

    // The shop's tokens: primary, secondary, warm paper, soft mint, paper
    // border, ink and its variants.
    private const string Teal = "#003441";
    private const string Coral = "#f36f5d";
    private const string Paper = "#fff8f1";
    private const string Card = "#ffffff";
    private const string Border = "#e5e0da";
    private const string Ink = "#1a1c1b";
    private const string Body = "#40484b";
    private const string Muted = "#70787c";
    private const string Mint = "#ddf3ef";
    private const string Footer = "#fffaf5";

    /// <summary>
    /// Persian-friendly faces that are actually installed somewhere.
    /// </summary>
    /// <remarks>
    /// No webfont link: several clients strip it, and the ones that do not
    /// would be fetching a font from a third party on the customer's behalf.
    /// The stack falls through to whatever the reader's device has.
    /// </remarks>
    private const string Face = "Vazirmatn,'Segoe UI',IRANSansX,Tahoma,Arial,sans-serif";

    /// <summary>
    /// Wraps content in the frame.
    /// </summary>
    /// <param name="preheader">
    /// The grey line an inbox shows beside the subject. Written deliberately,
    /// because a client that finds none takes the opening words of the body —
    /// which on a receipt is "از خرید شما سپاسگزاریم" for every message alike.
    /// </param>
    public static string Wrap(string heading, string preheader, string inner, string siteUrl, string supportUrl)
    {
        var year = DateTimeOffset.UtcNow.Year;

        return $"""
            <!DOCTYPE html>
            <html lang="fa" dir="rtl">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light">
            <meta name="supported-color-schemes" content="light">
            </head>
            <body style="margin:0;padding:0;background:{Paper};font-family:{Face};">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:{Paper};font-size:1px;line-height:1px;">{Escape(preheader)}</div>
            <table role="presentation" dir="rtl" width="100%" cellpadding="0" cellspacing="0" style="background:{Paper};padding:28px 12px;direction:rtl;">
            <tr><td align="center">
            <table role="presentation" dir="rtl" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;width:100%;background:{Card};border:1px solid {Border};border-radius:14px;overflow:hidden;direction:rtl;">
            <tr><td style="background:{Teal};padding:22px 26px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
            <td align="right" style="color:#ffffff;font-family:{Face};font-size:19px;font-weight:700;">{Brand}</td>
            <td align="left" dir="ltr" style="color:#9acee1;font-family:'Segoe UI',Arial,sans-serif;font-size:11px;letter-spacing:.04em;direction:ltr;">{BrandLatin}</td>
            </tr></table>
            </td></tr>
            <tr><td dir="rtl" align="right" style="padding:26px 26px 22px;color:{Body};font-family:{Face};font-size:14.5px;line-height:2.05;direction:rtl;text-align:right;">
            <h1 style="margin:0 0 12px;font-family:{Face};font-size:20px;font-weight:700;color:{Ink};line-height:1.6;text-align:right;">{Escape(heading)}</h1>
            {inner}
            </td></tr>
            <tr><td style="padding:16px 26px 20px;background:{Footer};border-top:1px solid {Border};text-align:center;font-family:{Face};font-size:12px;line-height:1.9;color:{Muted};">
            <a href="{Escape(supportUrl)}" style="color:{Teal};text-decoration:none;font-weight:600;">پشتیبانی</a>
            <span style="color:{Border};">&nbsp;·&nbsp;</span>
            <a href="{Escape(siteUrl)}" style="color:{Teal};text-decoration:none;font-weight:600;">bojanstore.com</a>
            <p style="margin:8px 0 0;">این ایمیل به‌صورت خودکار ارسال شده؛ لطفاً به آن پاسخ ندهید.<br>© {year} {Brand}</p>
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body></html>
            """;
    }

    /// <summary>A paragraph.</summary>
    public static string P(string text) =>
        $"""<p style="margin:0 0 12px;">{Escape(text)}</p>""";

    /// <summary>The one action. Every email has at most one.</summary>
    public static string Button(string label, string url) =>
        $"""
        <table role="presentation" align="center" cellpadding="0" cellspacing="0" style="margin:18px auto 6px;"><tr>
        <td align="center" style="border-radius:10px;background:{Coral};">
        <a href="{Escape(url)}" style="display:inline-block;padding:12px 34px;color:#ffffff;font-family:{Face};font-size:15px;font-weight:700;text-decoration:none;border-radius:10px;">{Escape(label)}</a>
        </td></tr></table>
        """;

    /// <summary>
    /// The same destination as plain text under the button.
    /// </summary>
    /// <remarks>
    /// Corporate clients strip the button outright, and without the address
    /// beside it the message becomes unusable — which for a password reset
    /// means locked out.
    /// </remarks>
    public static string LinkFallback(string url) =>
        $"""
        <p style="margin:10px 0 0;color:{Muted};font-size:12.5px;">اگر دکمه کار نکرد، این نشانی را در مرورگر باز کنید:</p>
        <p dir="ltr" style="margin:4px 0 0;word-break:break-all;direction:ltr;text-align:left;"><a href="{Escape(url)}" style="color:{Teal};font-size:12.5px;">{Escape(url)}</a></p>
        """;

    /// <summary>A tinted note. <paramref name="tone"/> picks which.</summary>
    public static string Note(string text, NoteTone tone = NoteTone.Plain)
    {
        var (background, border, colour) = tone switch
        {
            NoteTone.Good => (Mint, "#b9e3dc", "#14403a"),
            NoteTone.Warn => ("#fdeae6", "#f6cabf", "#7a2418"),
            _ => ("#faf8f5", "#eee8e1", Body),
        };

        return $"""
            <table role="presentation" dir="rtl" width="100%" cellpadding="0" cellspacing="0" style="margin:14px 0;direction:rtl;"><tr>
            <td dir="rtl" align="right" style="background:{background};border:1px solid {border};border-radius:10px;padding:13px 16px;color:{colour};font-family:{Face};font-size:13.5px;line-height:1.95;direction:rtl;text-align:right;">{text}</td>
            </tr></table>
            """;
    }

    /// <summary>
    /// A label/value block. Rows whose value is blank are dropped.
    /// </summary>
    /// <remarks>
    /// Dropped rather than printed empty: a label beside nothing reads as a
    /// field the shop forgot to fill in, and half these rows are genuinely
    /// optional — an order with no tracking code, a cancellation with no
    /// penalty.
    /// </remarks>
    public static string Rows(params (string Key, string? Value)[] rows)
    {
        var present = rows.Where(row => !string.IsNullOrWhiteSpace(row.Value)).ToList();
        if (present.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append($"""<table role="presentation" dir="rtl" width="100%" cellpadding="0" cellspacing="0" style="margin:14px 0;direction:rtl;">""");

        for (var index = 0; index < present.Count; index++)
        {
            var edge = index == present.Count - 1 ? "0" : $"1px solid #f0ece7";
            builder.Append($"""
                <tr>
                <td dir="rtl" align="right" style="padding:7px 14px 7px 0;border-bottom:{edge};color:{Muted};font-family:{Face};font-size:13.5px;white-space:nowrap;">{Escape(present[index].Key)}</td>
                <td dir="rtl" align="left" style="padding:7px 0;border-bottom:{edge};color:{Ink};font-family:{Face};font-size:13.5px;font-weight:600;text-align:left;">{Escape(present[index].Value!)}</td>
                </tr>
                """);
        }

        builder.Append("</table>");
        return builder.ToString();
    }

    /// <summary>One line of an order table.</summary>
    public sealed record Item(string Title, string Quantity, string Amount);

    /// <summary>The ordered goods, with a totals foot.</summary>
    public static string Items(IReadOnlyList<Item> items, params (string Key, string? Value)[] totals)
    {
        var builder = new StringBuilder();
        builder.Append($"""
            <table role="presentation" dir="rtl" width="100%" cellpadding="0" cellspacing="0" style="margin:14px 0;direction:rtl;border-collapse:collapse;">
            <tr>
            <th align="right" style="padding:8px 10px;background:#f7f4f0;border-bottom:1px solid {Border};color:{Body};font-family:{Face};font-size:12.5px;font-weight:700;">کالا</th>
            <th align="left" style="padding:8px 10px;background:#f7f4f0;border-bottom:1px solid {Border};color:{Body};font-family:{Face};font-size:12.5px;font-weight:700;white-space:nowrap;">تعداد</th>
            <th align="left" style="padding:8px 10px;background:#f7f4f0;border-bottom:1px solid {Border};color:{Body};font-family:{Face};font-size:12.5px;font-weight:700;white-space:nowrap;">مبلغ</th>
            </tr>
            """);

        foreach (var item in items)
        {
            builder.Append($"""
                <tr>
                <td align="right" style="padding:9px 10px;border-bottom:1px solid #f0ece7;color:{Ink};font-family:{Face};font-size:13.5px;">{Escape(item.Title)}</td>
                <td align="left" style="padding:9px 10px;border-bottom:1px solid #f0ece7;color:{Ink};font-family:{Face};font-size:13.5px;white-space:nowrap;">{Escape(item.Quantity)}</td>
                <td align="left" style="padding:9px 10px;border-bottom:1px solid #f0ece7;color:{Ink};font-family:{Face};font-size:13.5px;white-space:nowrap;">{Escape(item.Amount)}</td>
                </tr>
                """);
        }

        var present = totals.Where(total => !string.IsNullOrWhiteSpace(total.Value)).ToList();
        for (var index = 0; index < present.Count; index++)
        {
            // The last total is the one that matters, so it gets the rule above
            // it and the weight.
            var last = index == present.Count - 1;
            var top = index == 0 ? $"2px solid {Teal}" : "0";
            builder.Append($"""
                <tr>
                <td align="right" style="padding:{(index == 0 ? "11px" : "5px")} 10px 5px;border-top:{top};color:{Ink};font-family:{Face};font-size:13.5px;font-weight:{(last ? "700" : "400")};">{Escape(present[index].Key)}</td>
                <td></td>
                <td align="left" style="padding:{(index == 0 ? "11px" : "5px")} 10px 5px;border-top:{top};color:{Ink};font-family:{Face};font-size:13.5px;font-weight:{(last ? "700" : "400")};white-space:nowrap;">{Escape(present[index].Value!)}</td>
                </tr>
                """);
        }

        builder.Append("</table>");
        return builder.ToString();
    }

    /// <summary>
    /// Escapes text for HTML.
    /// </summary>
    /// <remarks>
    /// Every value that reaches a template goes through this. Most of them come
    /// from the shop's own data, but a product title, a cancellation reason and
    /// a customer's own name are all text somebody typed — and an email body is
    /// rendered by a mail client that will run what it is given.
    /// </remarks>
    public static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public enum NoteTone
    {
        Plain,
        Good,
        Warn,
    }
}
