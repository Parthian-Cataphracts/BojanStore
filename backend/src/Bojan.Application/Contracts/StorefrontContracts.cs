namespace Bojan.Application.Contracts;

/// <summary>
/// The storefront's DTOs, mirroring
/// <c>apps/storefront/src/lib/api/types.ts</c> field for field.
/// </summary>
/// <remarks>
/// <para>
/// Property names are load-bearing: the API serialises camelCase, so
/// <c>CompareAtPrice</c> reaches the browser as <c>compareAtPrice</c>, which is
/// exactly what the TypeScript interface declares. Renaming anything here
/// breaks a screen silently — the frontend does not validate shapes, it just
/// reads fields that are no longer there.
/// </para>
/// <para>
/// Nullable properties correspond to TypeScript's <c>?:</c>. The host is
/// configured to omit nulls when writing
/// (<c>JsonIgnoreCondition.WhenWritingNull</c>), so an absent value arrives as
/// <c>undefined</c> rather than <c>null</c> — the distinction the frontend's
/// own optional-chaining and truthiness checks are written against.
/// </para>
/// <para>
/// Money is a plain <see cref="long"/> count of Toman and dates are
/// <see cref="DateTimeOffset"/> serialised as ISO 8601 — never a Jalali string
/// (<c>BACKEND.md</c> section 1.2). The frontend converts for display.
/// </para>
/// </remarks>
public sealed record ProductDto(
    string Id,
    string Slug,
    string Title,
    string Brand,
    string BrandSlug,
    string CategorySlug,
    string CategoryName,
    long Price,
    long? CompareAtPrice,
    double Rating,
    int ReviewCount,
    int Stock,
    string Image,
    string ImageAlt,
    IReadOnlyList<string>? Gallery,
    string? Description,
    IReadOnlyList<ProductSpecDto>? Specs,
    bool IsNew,
    bool IsBestseller);

public sealed record ProductSpecDto(string Label, string Value);

public sealed record CategoryDto(
    string Slug,
    string Name,
    string Icon,
    int ProductCount,
    string? Image,
    IReadOnlyList<CategoryDto>? Children);

public sealed record BrandDto(
    string Slug,
    string Name,
    int ProductCount,
    string? Tagline,
    string? Description,
    string? Logo,
    string? Cover,
    bool? Featured);

public sealed record CollectionDto(
    string Slug,
    string Title,
    string Summary,
    string Cover,
    IReadOnlyList<string> ProductSlugs,
    string? EditorialNote,
    bool? Featured);

public sealed record ArticleDto(
    string Slug,
    string Title,
    string Excerpt,
    string Category,
    string Cover,
    DateTimeOffset PublishedAt,
    int ReadingMinutes,
    bool? Featured,
    IReadOnlyList<ArticleBlockDto>? Body,
    string? RecommendedProductSlug);

/// <summary>
/// One block of an article body. Matches the frontend's discriminated union
/// <c>{ type: 'paragraph' | 'heading' | 'product'; text: string }</c> — a
/// <c>product</c> block carries no text, so <see cref="Text"/> is omitted for it.
/// </summary>
public sealed record ArticleBlockDto(string Type, string? Text);

public sealed record AddressDto(
    string Id,
    string Title,
    string Recipient,
    string Phone,
    string Province,
    string City,
    string PostalCode,
    string Line,
    bool IsDefault);

public sealed record UserDto(
    string Id,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string? BirthDate,
    string? City,
    string? Avatar,
    long WalletBalance,
    int LoyaltyPoints);

public sealed record OrderSummaryDto(
    string Id,
    string Number,
    DateTimeOffset PlacedAt,
    string Status,
    int ItemCount,
    long Total,
    IReadOnlyList<string>? Thumbnails);

public sealed record OrderItemDto(
    string ProductId,
    string Slug,
    string Title,
    string Image,
    int Quantity,
    long UnitPrice);

public sealed record OrderTimelineStepDto(string Id, string Label, string State, DateTimeOffset? At);

public sealed record OrderDetailDto(
    string Id,
    string Number,
    DateTimeOffset PlacedAt,
    string Status,
    int ItemCount,
    long Total,
    IReadOnlyList<string>? Thumbnails,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderTimelineStepDto> Timeline,
    string ShippingAddress,
    string ShippingMethod,
    string PaymentMethod,
    long Subtotal,
    long Discount,
    long Shipping,
    string? TrackingCode);

public sealed record ReturnTimelineStepDto(string Id, string Label, string Description, string Icon, string State);

public sealed record ReturnRequestDto(
    string Id,
    string Code,
    string OrderId,
    string OrderNumber,
    string ProductSlug,
    string ProductTitle,
    string ProductImage,
    int Quantity,
    string Reason,
    string? Note,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReturnTimelineStepDto> Timeline);

public sealed record NotificationDto(
    string Id,
    string Kind,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    bool Read,
    string? Href);

public sealed record SupportTicketDto(
    string Id,
    string Subject,
    string Status,
    string LastMessage,
    bool LastMessageFromSupport,
    DateTimeOffset UpdatedAt);

public sealed record MyReviewDto(
    string Id,
    string ProductSlug,
    string ProductTitle,
    string ProductImage,
    int Rating,
    string Body,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record AwaitingReviewDto(
    string OrderId,
    string ProductSlug,
    string ProductTitle,
    string ProductImage,
    DateTimeOffset DeliveredAt);

public sealed record WalletTransactionDto(
    string Id,
    string Title,
    long Amount,
    DateTimeOffset CreatedAt,
    string Status,
    string Icon);

public sealed record CouponDto(
    string Id,
    string Code,
    string Title,
    string Condition,
    int? Percent,
    long? Amount,
    DateTimeOffset ExpiresAt,
    bool Used);

public sealed record ProductReviewDto(
    string Id,
    string Author,
    int Rating,
    string Body,
    DateTimeOffset CreatedAt,
    bool Verified,
    int HelpfulCount);

/// <summary>
/// Screen 84's rating histogram. <see cref="Counts"/> is keyed by the star
/// value as a string because the frontend types it
/// <c>Record&lt;1|2|3|4|5, number&gt;</c> — JSON object keys are strings either way.
/// </summary>
public sealed record RatingBreakdownDto(double Average, int Total, IReadOnlyDictionary<string, int> Counts);

public sealed record ProductQuestionAnswerDto(string Author, string Body, DateTimeOffset AnsweredAt);

public sealed record ProductQuestionDto(
    string Id,
    string Author,
    string Question,
    DateTimeOffset AskedAt,
    ProductQuestionAnswerDto? Answer);

public sealed record VariantOptionDto(string Id, string Label, string? Hex, bool Available);

public sealed record ProductVariantAxisDto(string Id, string Label, string Kind, IReadOnlyList<VariantOptionDto> Options);

public sealed record B2BTimelineStepDto(string Id, string Label, DateTimeOffset? At, string State);

public sealed record B2BRequestDto(
    string Id,
    string Code,
    string Title,
    string Kind,
    string Status,
    DateTimeOffset CreatedAt,
    string Organization,
    int ItemCount,
    string? QuoteId,
    IReadOnlyList<B2BTimelineStepDto> Timeline);

public sealed record QuoteLineDto(string Title, string Sku, int Quantity, long UnitPrice);

public sealed record QuoteDto(
    string Id,
    string Number,
    string RequestCode,
    string Organization,
    string SalesRep,
    DateTimeOffset ValidUntil,
    string Status,
    IReadOnlyList<QuoteLineDto> Lines,
    long Subtotal,
    long Discount,
    long Tax,
    long Total);

public sealed record GiftBundleDto(
    string Slug,
    string Title,
    string Summary,
    string Cover,
    string Category,
    long PricePerUnit,
    int MinimumQuantity);

/// <summary>
/// One row of <c>GET /shipping-methods</c>. <c>Id</c> is the method's code
/// (<c>standard</c>, <c>express</c>, <c>courier</c>) — see
/// <see cref="Domain.Orders.ShippingMethod.Code"/> for why it is not the GUID.
/// </summary>
public sealed record ShippingMethodDto(string Id, string Title, long Price, string? Estimate, string Icon);

public sealed record PaymentMethodDto(string Id, string Title, string? Note, string Icon, bool RequiresGateway);

/// <summary>
/// <c>POST /cart/coupon</c>'s response. <c>discount</c> is an absolute amount
/// in Toman, never a percentage — <c>BACKEND.md</c> Phase 4 is explicit that
/// the frontend subtracts it as given.
/// </summary>
public sealed record CouponResultDto(string Code, long Discount);

/// <summary>
/// <c>POST /orders</c>'s response. <c>paymentUrl</c> is present only for a
/// gateway redirect — the checkout already redirects to it when it is there,
/// so returning it for cash on delivery would send the shopper to a payment
/// page they do not owe.
/// </summary>
public sealed record PlacedOrderDto(string OrderNumber, string? PaymentUrl);
