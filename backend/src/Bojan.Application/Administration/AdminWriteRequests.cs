namespace Bojan.Application.Administration;

/// <summary>
/// One record per row of <c>BACKEND.md</c>'s Phase 7 table, carrying exactly
/// the fields that row lists.
/// </summary>
/// <remarks>
/// <para>
/// The panel's own allow-list
/// (<c>apps/admin/src/lib/api/resources.ts</c>) already drops anything not on
/// these lists before forwarding. Declaring them again as types is the second
/// half of that guarantee: a field the panel never sends has nowhere to land
/// here either, so a request that bypasses the panel entirely cannot set
/// <c>costPrice</c> through a form that does not show it.
/// </para>
/// <para>
/// Ids arrive as strings because that is how the panel holds them, and are
/// parsed at the edge of each handler — a malformed id is a 400, not an
/// exception in the model binder.
/// </para>
/// </remarks>
/// <remarks>
/// Every field the panel's product form shows, because a field the form
/// collects and this record does not declare is a field the operator fills and
/// the deserialiser drops — silently, with a 200 back. The names are the ones
/// <c>resources.ts</c> forwards, not the ones the entity happens to use.
/// </remarks>
public sealed record SaveProductRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Sku,
    string? Brand,
    /// <summary>
    /// The primary category, as a slug or an id.
    /// </summary>
    /// <remarks>
    /// Superseded by <see cref="Categories"/>, which the panel's form now
    /// posts, and kept because it is the field every caller written against
    /// the single-category API sends. When both arrive, the list wins and this
    /// is ignored — it says nothing the list does not.
    /// </remarks>
    string? Category,
    long? Price,
    long? CostPrice,
    /// <summary>List price struck through on the product card. Zero clears it.</summary>
    long? CompareAt,
    int? Stock,
    int? LowStock,
    bool? TrackStock,
    bool? Backorder,
    string? Status,
    string? Description,
    string? MetaTitle,
    string? MetaDescription,
    IReadOnlyList<string>? Images,
    /// <summary>
    /// Every category the product is filed under, primary first.
    /// </summary>
    /// <remarks>
    /// Posted whole rather than as a delta, like <see cref="Images"/>: the
    /// form's picker is a set of checkboxes, and an unticked box is only
    /// expressible as an absence from the list. An empty list is refused
    /// rather than obeyed — a product filed nowhere would disappear from
    /// browsing entirely, which is not something a save should be able to do
    /// silently.
    /// </remarks>
    IReadOnlyList<string>? Categories = null,
    /// <summary>
    /// Every collection the product belongs to, as slugs or ids.
    /// </summary>
    /// <remarks>
    /// Membership was only ever writable from the collection's side, and there
    /// is no screen there for it either — so a curated grouping could be
    /// created in the panel and never filled. Editing it from the product is
    /// the direction an operator actually works in: they have the product open
    /// and know which groupings it belongs to.
    ///
    /// An empty list is honoured here, unlike <see cref="Categories"/>:
    /// belonging to no collection is an ordinary state for a product, and
    /// clearing the last one has to be possible.
    /// </remarks>
    IReadOnlyList<string>? Collections = null);

// --- product detail screens (106, 107, 108) --------------------------------

/// <remarks>
/// Each of the three posts the product's whole list, not a delta. The screens
/// edit a table in place — add a row, change one, delete one — and there is no
/// point in the flow where a single row is the unit of work. Replacing
/// wholesale also makes a deletion a deletion, which a stream of upserts cannot
/// express.
/// </remarks>
public sealed record VariantOptionRequest(string Key, string Label, string? Hex, bool? Available);

public sealed record VariantAxisRequest(
    string Key,
    string Label,
    string? Kind,
    IReadOnlyList<VariantOptionRequest> Options);

public sealed record SaveVariantsRequest(string Id, IReadOnlyList<VariantAxisRequest> Axes);

public sealed record SkuRequest(
    string Code,
    string? Barcode,
    string? Combination,
    long? Price,
    int? Stock,
    bool? Active);

public sealed record SaveSkusRequest(string Id, IReadOnlyList<SkuRequest> Skus);

public sealed record AttributeRequest(
    string Name,
    string? Kind,
    IReadOnlyList<string>? Values,
    bool? Filterable);

public sealed record SaveAttributesRequest(string Id, IReadOnlyList<AttributeRequest> Attributes);

/// <summary>One line of a pro-forma an operator is issuing.</summary>
/// <param name="UnitPriceOverride">
/// Null lets the product's volume ladder decide, which is the normal case. A
/// value is a rep pricing this line by hand — a negotiated figure the ladder
/// does not know about.
/// </param>
public sealed record IssueQuoteLine(Guid ProductId, int Quantity, long? UnitPriceOverride = null);

/// <summary>What the panel submits to turn a business request into a pro-forma.</summary>
public sealed record IssueQuoteRequest(
    string RequestId,
    IReadOnlyList<IssueQuoteLine> Lines,
    long Discount = 0,
    int TaxRatePercent = 9,
    DateTimeOffset? ValidUntilUtc = null);

/// <summary>
/// One rung of a product's volume ladder, as the panel edits it.
/// </summary>
/// <remarks>
/// Floors rather than ranges — see <c>ProductVolumeTier</c> for why a ladder
/// that cannot leave a gap is the safer shape to let an operator type.
/// </remarks>
public sealed record ProductVolumeTierDto(int MinimumQuantity, int DiscountPercent);

/// <summary>The whole ladder for one product, replaced in a single write.</summary>
public sealed record SaveProductVolumeTiersRequest(string Id, IReadOnlyList<ProductVolumeTierDto> Tiers);

/// <summary>
/// What a collection holds, and in what order — screen 104's products panel.
/// </summary>
/// <remarks>
/// The whole list, not a delta, like the product detail screens: the panel
/// adds, removes and reorders in place, and none of those is expressible as a
/// stream of single-row edits. Position in <c>Products</c> is the order the
/// storefront renders, so the list arriving complete is what makes reordering
/// mean anything.
///
/// Products are slugs or ids, whichever the caller holds — the same as every
/// other catalogue reference in this file.
/// </remarks>
public sealed record SaveCollectionProductsRequest(string Id, IReadOnlyList<string> Products);

/// <summary>
/// A product as the quote composer offers it: what it costs, and the ladder
/// under that price.
/// </summary>
/// <remarks>
/// The ladder travels with the product so the screen can show a rep what a
/// hundred units come to before they issue anything. The figures are still
/// recomputed server-side when the quote is submitted — this is what the
/// operator sees, never what they are trusted to send back.
/// </remarks>
public sealed record AdminQuotableProductDto(
    string Id,
    string Title,
    string Sku,
    long Price,
    IReadOnlyList<ProductVolumeTierDto> Tiers);


public sealed record ProductPricingRequest(string Id, long? Price, long? CostPrice, long? CompareAtPrice);

public sealed record ProductDiscountRequest(
    string Id,
    int? Percent,
    long? Amount,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

public sealed record SaveCategoryRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? ParentId,
    string? Description,
    string? Icon,
    string? Status,
    string? MetaTitle,
    string? MetaDescription,
    bool? ShowInMenu,
    int? Order);

public sealed record SaveBrandRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Tagline,
    string? Country,
    string? Description,
    string? Logo,
    string? Status,
    string? MetaTitle,
    string? MetaDescription,
    bool? Featured);

public sealed record SaveCollectionRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Description,
    string? Summary,
    string? EditorialNote,
    string? Cover,
    string? Status,
    bool? Featured);

public sealed record SaveContentRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Kind,
    string? Body,
    string? Excerpt,
    string? Cover,
    string? Status);

public sealed record SaveCampaignRequest(
    string? Id,
    string? Title,
    string? Kind,
    string? Status,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? Description);

public sealed record SaveCouponRequest(
    string? Id,
    string? Code,
    int? Percent,
    long? Amount,
    long? MinimumSpend,
    DateTimeOffset? ExpiresAt,
    string? Status);

public sealed record StockMovementRequest(string ProductId, string Kind, int Quantity, string Reason, string? Reference);

public sealed record OrderStatusRequest(string Id, string Status, string? Note, string? TrackingCode);

public sealed record BusinessRequestUpdate(string Id, string? Status, string? AssigneeId, string? Note);

public sealed record SupportReplyRequest(string ThreadId, string Body);

public sealed record CannedReplyRequest(string? Id, string? Title, string? Body, bool? Deleted);

public sealed record BroadcastRequest(string Channel, string Audience, string Title, string Body, DateTimeOffset? ScheduledAt);

/// <summary>
/// One in-app notification for one customer — the panel's "notify this
/// customer" action on the customer detail screen.
/// </summary>
/// <remarks>
/// Separate from <see cref="BroadcastRequest"/> rather than a broadcast with an
/// audience of one. It has no channel (in-app is the only thing that makes
/// sense for a message about this person's own account), no schedule, and it
/// carries a <paramref name="Link"/> a broadcast does not — and it should not
/// leave a campaign row behind, because it is not a campaign.
/// </remarks>
/// <param name="Link">
/// Where tapping it goes. A site-relative path or nothing: see
/// <c>CustomerNotification.IsInternalPath</c> for why an operator-supplied
/// destination cannot be taken on trust.
/// </param>
public sealed record CustomerNotificationRequest(string CustomerId, string Title, string Body, string? Link);

/// <summary>
/// Suspending a customer, or letting them back in.
/// </summary>
/// <param name="Blocked">
/// The state asked for rather than a toggle. Two operators on the same screen
/// pressing the same button should agree about the result, and "flip it" does
/// not: whoever arrives second undoes the first.
/// </param>
public sealed record CustomerBlockRequest(string CustomerId, bool Blocked);

/// <summary>
/// Setting a customer's password on their behalf — the counter answer for
/// somebody who can receive neither the code nor the reset mail.
/// </summary>
/// <remarks>
/// The password is written, never read back: nothing returns it, and the audit
/// line records only that it happened and against whom.
/// </remarks>
public sealed record CustomerPasswordRequest(string CustomerId, string Password);

/// <summary>
/// Editing a customer's record from the panel.
/// </summary>
/// <remarks>
/// <para>
/// Every field on the record except the money. The wallet balance and the
/// loyalty points are ledgers — they move by a transaction that says why, and a
/// form that could set them directly would be a way to credit an account with
/// no record of where the credit came from.
/// </para>
/// <para>
/// A null field is left alone; an empty string clears an optional one. That is
/// what lets a form send only what it changed and still be able to remove a
/// city somebody typed by mistake.
/// </para>
/// </remarks>
public sealed record SaveCustomerRequest(
    string Id,
    string? Code,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    string? City,
    string? NationalId,
    string? Group,
    /// <summary>ISO <c>YYYY-MM-DD</c>; empty clears it.</summary>
    string? BirthDate);

/// <summary>
/// Removing a customer outright.
/// </summary>
/// <remarks>
/// Refused for an account that has traded — an order, a wallet movement or a
/// support ticket restricts it, because an invoice whose customer is gone is
/// not a tidier invoice. Those accounts get suspended instead.
/// </remarks>
public sealed record DeleteCustomerRequest(string CustomerId);

/// <summary>
/// Removing a sent message from «ارسال اعلان».
/// </summary>
/// <remarks>
/// <see cref="Kind"/> is required because the id alone does not say which table
/// the row is in — a broadcast and a one-customer message are different
/// entities that happen to share a list on screen.
/// </remarks>
public sealed record DeleteNotificationRequest(string Kind, string Id);

public sealed record ReportExportRequest(string Report, string? Format, DateTimeOffset? From, DateTimeOffset? To);

/// <summary><c>values</c> is an arbitrary JSON object; the section decides what is in it.</summary>
public sealed record SettingsRequest(string Section, IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Screen 95's settlement control — an operator recording that an order's money
/// arrived, or that the attempt was refused.
/// </summary>
/// <remarks>
/// No amount field. What the order is worth is already recorded on the order,
/// and a body that could name its own figure would be a way to mark an order
/// paid for less than it costs. <c>Reference</c> is whatever identifies the
/// transfer — a gateway reference or the tracking number off a bank slip — and
/// is stored so the settlement can be matched against a statement later.
/// </remarks>
public sealed record OrderPaymentRequest(string Id, bool Paid, string? Reference);

public sealed record BackupRequest(string Kind, bool Confirm);

/// <summary>One cell of screen 146's grid, as the panel's save button sends the whole matrix.</summary>
public sealed record RoleGrantRequest(string Role, string Section, bool Granted);

/// <summary><c>POST /admin/roles/permissions</c>'s body — the whole matrix, saved as one replace.</summary>
public sealed record RoleGrantsBody(IReadOnlyList<RoleGrantRequest> Grants);

public sealed record ApiKeyRequest(string? Id, string? Label, string? Scope, bool? Revoked);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// A magazine article, from the panel — screens 122 and 123.
/// </summary>
/// <remarks>
/// <para>
/// Writes <c>Article</c>, which is the table the storefront's magazine reads.
/// The panel used to save these as <c>ContentEntry</c> with <c>kind: article</c>
/// — a different table nothing on the site serves, so a published article
/// appeared nowhere and every article that did appear was invisible here.
/// </para>
/// <para>
/// <c>Body</c> is plain text: a blank line separates paragraphs and a line
/// starting <c>##</c> is a heading. The storefront renders typed blocks and the
/// service translates, because the alternative is a block editor and this is a
/// shop, not a newsroom.
/// </para>
/// <para>
/// <c>ReadingMinutes</c> is not a field. It is derived from the body — a fact
/// about the text rather than a decision, and one more thing to fall out of
/// step with an edit if it were typed.
/// </para>
/// </remarks>
public sealed record SaveArticleRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Excerpt,
    string? Category,
    string? Cover,
    string? Body,
    bool? Featured,
    /// <summary><c>published</c>, <c>draft</c>, or <c>archived</c> to take it off the site.</summary>
    string? Status);

/// <summary>An operator's verdict on one review — <c>pending</c>, <c>published</c> or <c>rejected</c>.</summary>
public sealed record ReviewModerationRequest(string Id, string Status);

/// <summary>The «نمایش در صفحه اصلی» tick on one review.</summary>
public sealed record ReviewFeatureRequest(string Id, bool Featured);

/// <summary>Removing a review outright, as opposed to rejecting it.</summary>
public sealed record DeleteReviewRequest(string Id);

/// <summary>
/// Appointing an operator, or editing one — screen 145's form, which posts the
/// same body either way.
/// </summary>
/// <remarks>
/// <para>
/// <c>Id</c> absent is a create and everything else is required; <c>Id</c>
/// present is an edit and every other field is optional, so a form that only
/// changes a role sends only a role. The same upsert shape the catalogue saves
/// use.
/// </para>
/// <para>
/// <c>Password</c> is the initial one and is accepted only on the create
/// branch. Replacing an existing operator's password has to end the sessions
/// open on the old one, and that is <see cref="AdminUserPasswordRequest"/>'s
/// job rather than a side effect of renaming somebody.
/// </para>
/// <para>
/// <c>IsActive</c> is the state asked for rather than a toggle — see
/// <see cref="CustomerBlockRequest"/> for why. There is no delete: this account
/// is the foreign key on the audit trail, so an operator who leaves is
/// suspended and the record of what they did stays readable.
/// </para>
/// </remarks>
public sealed record SaveAdminUserRequest(
    string? Id,
    string? Name,
    string? Email,
    string? Phone,
    string? Role,
    bool? IsActive,
    string? Password,
    /// <summary>
    /// On create: the phone or e-mail of an existing shop account to promote.
    /// </summary>
    /// <remarks>
    /// An operator is not created here any more, it is appointed. The person
    /// registered on the site and owns their own password; this screen chooses
    /// who and what, never a credential — which is why <c>Password</c> above is
    /// ignored on create and kept only so an older client posting it is not a
    /// binding failure.
    /// </remarks>
    string? Identity = null,
    /// <summary>Panel section keys this operator may open. Empty leaves the role unnarrowed.</summary>
    IReadOnlyList<string>? Sections = null);

/// <summary>
/// Setting another operator's password, for one who is locked out and has no
/// self-service route back.
/// </summary>
/// <remarks>
/// Never the caller's own — <c>POST /me/password</c> is that, and it asks for
/// the current password first. The password is written and never read back; the
/// audit line records only that it happened and against whom.
/// </remarks>
public sealed record AdminUserPasswordRequest(string Id, string Password);

/// <summary>
/// Lifting another operator's second factor — the lost-authenticator rescue.
/// </summary>
/// <remarks>
/// Only an id: there is nothing to decide. Turning one back on is the
/// operator's own enrolment on <c>POST /me/2fa</c>, which needs a code from the
/// authenticator they will by then have.
/// </remarks>
public sealed record AdminUserTwoFactorRequest(string Id);

public sealed record TwoFactorRequest(string Code, string? Secret);

/// <summary>
/// An operator's decision on a card-to-card top-up.
/// </summary>
/// <remarks>
/// The customer is not a field here, and neither is the amount: both are read
/// from the stored request. A decision endpoint that took an amount would be a
/// way to credit a wallet with a number of the caller's choosing.
/// </remarks>
public sealed record WalletTopUpDecisionRequest(string Id, bool Approve, string? Note);

/// <summary>
/// An operator cancelling an order.
/// </summary>
/// <remarks>
/// Neither the refund nor the penalty is a field: both are derived from the
/// order's own recorded figures and the stage it reached. A cancel endpoint
/// that took an amount would be a way to pay a wallet whatever the caller
/// fancied, which is the same reason
/// <see cref="WalletTopUpDecisionRequest"/> carries no amount either.
/// <para>
/// <c>ChargePenalty</c> defaults to true — the customer asked. The operator
/// clears it when the shop is at fault (out of stock after confirmation, a
/// pricing error), because the percentage is meant to cover work the customer's
/// change of mind wasted, not the shop's own.
/// </para>
/// </remarks>
public sealed record OrderCancellationRequest(string Id, string? Reason, bool ChargePenalty = true);

/// <summary>
/// An operator deciding a return — screens 35 and 36's operator side.
/// </summary>
/// <remarks>
/// <para>
/// No amount, for the reason <see cref="OrderCancellationRequest"/> carries
/// none: what a return is worth is derived from the order's own frozen line
/// prices, and a body that could name its own figure would be a way to pay a
/// wallet whatever the caller fancied.
/// </para>
/// <para>
/// <c>Restock</c> defaults to true — goods normally come back sellable — and is
/// read only at the warehouse step. It is the one judgement here that a person
/// has to make: the parcel has been out of the shop's hands, so whether its
/// contents go back on the shelf is something someone has to look at, not
/// something a status change can imply.
/// </para>
/// </remarks>
public sealed record ReturnDecisionRequest(string Id, string Status, string? Note, bool Restock = true);
