namespace Bojan.Application.Contracts;

/// <summary>
/// Everything the storefront shows that belongs to the shop rather than to the
/// catalogue.
/// </summary>
/// <remarks>
/// <para>
/// The header's name, the footer's contact block, the delivery promise on a
/// product page, the free-shipping line in the cart. All of it was written into
/// the components — a shop that moved office, changed its support number or
/// raised its free-shipping threshold needed a developer and a deploy, which is
/// exactly the arrangement this exists to end.
/// </para>
/// <para>
/// One request rather than a dozen keys read individually: it is rendered on
/// every page, so it is one indexed read of one section, cached at the edge like
/// the rest of the public reads.
/// </para>
/// </remarks>
public sealed record StorefrontSettingsDto(
    StoreIdentityDto Identity,
    StoreContactDto Contact,
    StoreSocialDto Social,
    StorePromisesDto Promises,
    IReadOnlyList<StoreTrustSealDto> TrustSeals);

/// <summary>
/// One trust mark in the footer — an Enamad badge, a payment licence, a
/// membership of something worth naming.
/// </summary>
/// <remarks>
/// <para>
/// A list rather than a fixed set of fields, because which marks a shop holds is
/// the shop's business and changes: one that earns its Enamad in a year must not
/// need a developer to say so, and one that lets a licence lapse must be able to
/// take the claim down the same afternoon. Claiming a certification the shop does
/// not hold is the kind of mistake that costs more than a deploy.
/// </para>
/// <para>
/// <paramref name="Enabled"/> is separate from removing the row: a mark under
/// renewal comes off the footer for a fortnight and goes back on, and retyping it
/// from memory is how the registration number ends up wrong.
/// </para>
/// </remarks>
public sealed record StoreTrustSealDto(string Title, string Subtitle, string Link, bool Enabled);

/// <summary>What the shop calls itself — the header, the footer, page titles.</summary>
public sealed record StoreIdentityDto(string Name, string Tagline, string Description);

/// <summary>
/// How to reach the shop.
/// </summary>
/// <param name="BusinessPhone">
/// The organisational sales line, when the shop runs one. Empty falls back to
/// the main number rather than showing an empty row.
/// </param>
public sealed record StoreContactDto(
    string Phone,
    string Email,
    string BusinessPhone,
    string BusinessEmail,
    string Address,
    string PostalCode,
    string WorkingHours);

/// <summary>
/// The shop's accounts elsewhere.
/// </summary>
/// <remarks>
/// Handles or full URLs, whichever the owner typed — the storefront renders
/// only the ones that are set, so a shop with no Telegram simply has no Telegram
/// icon rather than one that goes nowhere.
/// </remarks>
public sealed record StoreSocialDto(string Instagram, string Telegram, string WhatsApp, string LinkedIn);

/// <summary>
/// The promises the storefront makes in prose, as numbers.
/// </summary>
/// <param name="ReturnWindowDays">
/// How long after delivery a return may be asked for. Quoted on every product
/// page and in the returns policy.
/// </param>
public sealed record StorePromisesDto(
    int ReturnWindowDays,
    string DeliveryEstimate,
    string SupportPromise);

/// <summary>
/// One informational page as the owner wrote it — terms, privacy, the returns
/// policy, the shipping policy, the buying guide.
/// </summary>
/// <remarks>
/// <para>
/// These pages were compiled into the storefront bundle. Every word of the
/// returns policy, the delivery promises, the terms a customer agrees to by
/// buying — none of it could be changed without a developer and a deploy, which
/// for a document with legal weight is the wrong arrangement entirely.
/// </para>
/// <para>
/// The storefront still ships its own copy and falls back to it. A shop that has
/// not written its own terms shows the ones it launched with rather than an
/// empty page, and the moment the owner saves a page here it takes over.
/// </para>
/// </remarks>
public sealed record ContentPageDto(string Slug, string Title, string? Excerpt, string Body, DateTimeOffset UpdatedAt);

/// <summary>
/// One question and its answer, as the owner wrote them.
/// </summary>
/// <remarks>
/// The panel has had an FAQ editor since screen 125 and the storefront rendered
/// a list compiled into its own bundle, so every question an operator wrote went
/// nowhere and every question a customer read was unchangeable.
///
/// <paramref name="Category"/> is the entry's excerpt field. Free text rather
/// than a fixed set: the six groups the design drew are this shop's, and the
/// chips on the page are built from whatever categories the questions actually
/// carry.
/// </remarks>
public sealed record FaqEntryDto(string Question, string Answer, string Category);

/// <summary>
/// A promotional banner the owner set — today, the storefront's hero.
/// </summary>
/// <remarks>
/// The last of the four content kinds the panel could write and nothing could
/// read. The heading, the sentence under it and the picture behind it were the
/// largest and most-seen text on the shop and the only way to change any of it
/// was to edit a component.
///
/// The call to action is not here. Where it points is a route this application
/// owns, and a link an operator can type is a link that can be typed wrong — so
/// the wording and destination stay with the page and only what it says about
/// the shop is editable.
/// </remarks>
public sealed record BannerDto(string Slug, string Title, string Subtitle, string ImageUrl);

/// <summary>
/// One rung of the loyalty club, as the panel edits it and the storefront shows
/// it.
/// </summary>
/// <param name="FreeShipping">
/// Whether this tier's members are never charged for delivery, whatever method
/// they pick. The page advertised "ارسال رایگان نامحدود" for two years with
/// nothing behind it; this is the field that makes it true.
/// </param>
public sealed record LoyaltyTierDto(
    string Name,
    int MinimumPoints,
    int DiscountPercent,
    bool FreeShipping,
    int SortOrder = 0);

/// <summary>
/// The club as a whole.
/// </summary>
/// <param name="TomanPerPoint">
/// What a member spends to earn one point. Zero means the club has been paused —
/// balances stand, nothing new accrues.
/// </param>
/// <param name="Enabled">
/// Whether the storefront should show the club at all. False when no tier has
/// been configured, so a shop that has not set one up advertises nothing rather
/// than an empty ladder.
/// </param>
public sealed record LoyaltyProgrammeDto(
    bool Enabled,
    int TomanPerPoint,
    IReadOnlyList<LoyaltyTierDto> Tiers);

/// <summary>What the panel's loyalty screen submits — the whole ladder, replaced.</summary>
public sealed record SaveLoyaltyRequest(int TomanPerPoint, IReadOnlyList<LoyaltyTierDto> Tiers);
