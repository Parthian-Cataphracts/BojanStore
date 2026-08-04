using System.Text.Json;

namespace Bojan.Application.Contracts;

/// <summary>Who the invoice says is selling — the block screen 34 draws opposite the buyer.</summary>
public sealed record InvoiceSellerDto(
    string Name,
    string Website,
    string Email,
    string Phone,
    string Address,
    /// <summary>شناسه ملی — printed only when the shop has one to print.</summary>
    string NationalId,
    /// <summary>کد اقتصادی — same.</summary>
    string EconomicCode);

/// <summary>
/// The parts of the invoice that are the shop's words rather than the order's
/// facts.
/// </summary>
/// <remarks>
/// <para>
/// Everything here was hard-coded in the document component, which made the
/// shop's own legal footer a code change. It is the owner's copy: a seller
/// address, a support address, the terms a buyer is told they accepted. None of
/// it is derivable from an order, and all of it changes without a deploy.
/// </para>
/// <para>
/// Served on the invoice itself rather than fetched separately by whoever draws
/// it, so the customer's copy and the panel's cannot be rendered from different
/// settings — the same reason both read one <see cref="InvoiceDto"/>.
/// </para>
/// </remarks>
public sealed record InvoiceSettingsDto(
    InvoiceSellerDto Seller,
    /// <summary>The line above the terms — "از اعتماد و خرید شما سپاسگزاریم."</summary>
    string ThanksNote,
    string Terms,
    /// <summary>The footer line under the rule.</summary>
    string FooterNote,
    /// <summary>
    /// The uploaded electronic stamp, or null.
    /// </summary>
    /// <remarks>
    /// Null is not a missing value to fill in with a default: there is no
    /// default stamp, and an invented one on a document that settles money
    /// would be a forgery rather than a placeholder. The document draws an
    /// empty box to stamp by hand until a real file is uploaded.
    /// </remarks>
    string? StampUrl)
{
    /// <summary>The settings section these live under.</summary>
    public const string Section = "invoice";

    /// <summary>
    /// What the invoice says when nothing has been configured.
    /// </summary>
    /// <remarks>
    /// A shop that has never opened the settings screen still prints a complete
    /// document — the same rule the cancellation percentage follows. These are
    /// the values the component carried before they were configurable.
    /// </remarks>
    public static InvoiceSettingsDto Defaults { get; } = new(
        new InvoiceSellerDto(
            Name: "فروشگاه بوژان",
            Website: "bojanstore.com",
            Email: "support@bojanstore.com",
            Phone: string.Empty,
            Address: string.Empty,
            NationalId: string.Empty,
            EconomicCode: string.Empty),
        ThanksNote: "از اعتماد و خرید شما سپاسگزاریم.",
        Terms: "خریدار با ثبت این سفارش، قوانین و مقررات فروشگاه بوژان را مطالعه کرده و پذیرفته است. "
             + "مهلت مرجوعی کالا و شرایط گارانتی، مطابق شرایط اعلام‌شده در زمان خرید است.",
        FooterNote: "این فاکتور به‌صورت الکترونیکی صادر شده و بدون مهر و امضای فیزیکی نیز معتبر است.",
        StampUrl: null);

    /// <summary>
    /// Builds the settings from the stored rows, falling back per field.
    /// </summary>
    /// <param name="stored">
    /// Section rows as <c>key -&gt; JSON</c>. Values are JSON-encoded strings,
    /// quotes included — see <c>SettingEntry.Value</c>.
    /// </param>
    /// <remarks>
    /// Per field rather than all-or-nothing: an owner who set only a support
    /// address must not lose the rest of the document to blanks, and a key this
    /// version does not know about is ignored rather than fatal.
    /// </remarks>
    public static InvoiceSettingsDto From(IReadOnlyDictionary<string, string> stored)
    {
        string Read(string key, string fallback)
        {
            if (!stored.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            try
            {
                // Written by the panel as JSON. A row hand-edited to bare text
                // is read as that text rather than throwing — the settings
                // table is not a place to be brittle about quoting.
                var value = JsonSerializer.Deserialize<string>(raw);
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch (JsonException)
            {
                return raw;
            }
        }

        var defaults = Defaults;

        return new InvoiceSettingsDto(
            new InvoiceSellerDto(
                Read("sellerName", defaults.Seller.Name),
                Read("sellerWebsite", defaults.Seller.Website),
                Read("sellerEmail", defaults.Seller.Email),
                Read("sellerPhone", defaults.Seller.Phone),
                Read("sellerAddress", defaults.Seller.Address),
                Read("sellerNationalId", defaults.Seller.NationalId),
                Read("sellerEconomicCode", defaults.Seller.EconomicCode)),
            Read("thanksNote", defaults.ThanksNote),
            Read("terms", defaults.Terms),
            Read("footerNote", defaults.FooterNote),
            // The one field with no fallback: absent means no stamp, and the
            // document draws the empty box instead.
            Read("stampUrl", string.Empty) is { Length: > 0 } stamp ? stamp : null);
    }
}
