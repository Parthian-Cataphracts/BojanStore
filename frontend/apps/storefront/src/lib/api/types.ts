/**
 * Contracts shared with the .NET 10 backend.
 *
 * Keep these in sync with the API's DTOs. If the backend ends up publishing an
 * OpenAPI document, generate this file from it instead of hand-maintaining it.
 */

export interface Money {
  amount: number;
  currency: 'IRT';
}

export interface Product {
  id: string;
  slug: string;
  title: string;
  brand: string;
  brandSlug: string;
  categorySlug: string;
  categoryName: string;
  /** Selling price in Toman. */
  price: number;
  /** List price before discount, when the product is on sale. */
  compareAtPrice?: number;
  rating: number;
  reviewCount: number;
  stock: number;
  image: string;
  imageAlt: string;
  /** Additional gallery images; the card only ever uses `image`. */
  gallery?: string[];
  description?: string;
  specs?: ProductSpec[];
  isNew: boolean;
  isBestseller: boolean;
}

export interface ProductSpec {
  label: string;
  value: string;
}

export interface Category {
  slug: string;
  name: string;
  /** Material Symbols name used by the category tiles. */
  icon: string;
  productCount: number;
  image?: string;
  children?: Category[];
}

export interface Brand {
  slug: string;
  name: string;
  productCount: number;
  /** Short positioning line under the brand name on screen 26. */
  tagline?: string;
  description?: string;
  logo?: string;
  cover?: string;
  featured?: boolean;
}

/** Curated product groupings — screens 21 and 22. */
export interface Collection {
  slug: string;
  title: string;
  summary: string;
  cover: string;
  productSlugs: string[];
  /** Pull quote shown in the editorial note on the detail screen. */
  editorialNote?: string;
  featured?: boolean;
}

/** Magazine articles — screens 28 and 29. */
export interface Article {
  slug: string;
  title: string;
  excerpt: string;
  category: string;
  cover: string;
  publishedAt: string;
  /** Reading time in minutes. */
  readingMinutes: number;
  featured?: boolean;
  body?: ArticleBlock[];
  /** Product promoted inside the article body. */
  recommendedProductSlug?: string;
}

export type ArticleBlock =
  | { type: 'paragraph'; text: string }
  | { type: 'heading'; text: string }
  | { type: 'product' };

export interface CartLine {
  id: string;
  productId: string;
  /** The chosen combination's SKU (screen 108) — absent for a product with no variants. */
  skuId?: string;
  slug: string;
  title: string;
  brand: string;
  image: string;
  unitPrice: number;
  compareAtPrice?: number;
  quantity: number;
}

export interface Cart {
  id: string;
  lines: CartLine[];
  /** Sum of line totals before discount. */
  subtotal: number;
  discount: number;
  shipping: number;
  total: number;
  couponCode?: string;
}

export interface Address {
  id: string;
  title: string;
  recipient: string;
  phone: string;
  province: string;
  city: string;
  postalCode: string;
  line: string;
  isDefault: boolean;
}

export type OrderStatus =
  | 'pending'
  | 'processing'
  | 'shipped'
  | 'delivered'
  | 'cancelled'
  | 'returned';

export interface OrderSummary {
  id: string;
  number: string;
  placedAt: string;
  status: OrderStatus;
  itemCount: number;
  total: number;
  /** Thumbnails shown on the order card in screen 12. */
  thumbnails?: string[];
}

export interface OrderItem {
  productId: string;
  slug: string;
  title: string;
  image: string;
  quantity: number;
  unitPrice: number;
}

/** One step of the fulfilment timeline drawn on screen 13. */
export interface OrderTimelineStep {
  id: string;
  label: string;
  /** `done` and `current` render filled; `upcoming` renders hollow. */
  state: 'done' | 'current' | 'upcoming';
  at?: string;
}

export interface OrderDetail extends OrderSummary {
  items: OrderItem[];
  timeline: OrderTimelineStep[];
  shippingAddress: string;
  shippingMethod: string;
  paymentMethod: string;
  subtotal: number;
  discount: number;
  shipping: number;
  trackingCode?: string;
}

export type ReturnStatus = 'submitted' | 'reviewing' | 'approved' | 'received' | 'refunded' | 'rejected';

/** Return request — screens 35 and 36. */
export interface ReturnRequest {
  id: string;
  /** Human-facing code, e.g. `RT-BZ-204`. */
  code: string;
  orderId: string;
  orderNumber: string;
  productSlug: string;
  productTitle: string;
  productImage: string;
  quantity: number;
  reason: string;
  note?: string;
  status: ReturnStatus;
  createdAt: string;
  /** Five-step tracker drawn on screen 36. */
  timeline: ReturnTimelineStep[];
}

export interface ReturnTimelineStep {
  id: string;
  label: string;
  description: string;
  icon: string;
  state: 'done' | 'current' | 'upcoming';
}

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  phone: string;
  email?: string;
  /** ISO date; the profile form renders it as a Jalali date. */
  birthDate?: string;
  city?: string;
  avatar?: string;
  walletBalance: number;
  loyaltyPoints: number;
}

/** Screens 61-70 — Business (B2B) requests and quotes. */
export type B2BRequestStatus = 'submitted' | 'reviewing' | 'quoted' | 'approved' | 'rejected';

export interface B2BRequest {
  id: string;
  /** Human-facing code, e.g. `B2B-8902`. */
  code: string;
  title: string;
  kind: 'organization' | 'bulk' | 'promotional';
  status: B2BRequestStatus;
  createdAt: string;
  organization: string;
  itemCount: number;
  /** Set once a quote has been issued. */
  quoteId?: string;
  timeline: { id: string; label: string; at?: string; state: 'done' | 'current' | 'upcoming' }[];
}

export interface QuoteLine {
  title: string;
  sku: string;
  quantity: number;
  unitPrice: number;
}

export interface Quote {
  id: string;
  /** e.g. `QT-1405-8942`. */
  number: string;
  requestCode: string;
  organization: string;
  salesRep: string;
  validUntil: string;
  status: 'pending' | 'approved' | 'expired';
  lines: QuoteLine[];
  subtotal: number;
  discount: number;
  tax: number;
  total: number;
}

/** Screen 66 — Corporate gift bundles. */
export interface GiftBundle {
  slug: string;
  title: string;
  summary: string;
  cover: string;
  category: string;
  pricePerUnit: number;
  minimumQuantity: number;
}

/** Screen 84 — Reviews shown on a product. */
export interface ProductReview {
  id: string;
  author: string;
  rating: number;
  body: string;
  createdAt: string;
  /** Verified purchase badge on the review card. */
  verified: boolean;
  helpfulCount: number;
}

export interface RatingBreakdown {
  average: number;
  total: number;
  /** Count per star, indexed 5 → 1. */
  counts: Record<1 | 2 | 3 | 4 | 5, number>;
}

/** Screen 85 — Product questions and answers. */
export interface ProductQuestion {
  id: string;
  author: string;
  question: string;
  askedAt: string;
  answer?: { author: string; body: string; answeredAt: string };
}

/** Screen 86 — Variant axes on the product page. */
export interface ProductVariantAxis {
  id: string;
  label: string;
  /** `swatch` renders colour dots, `chip` renders text pills. */
  kind: 'swatch' | 'chip';
  options: { id: string; label: string; hex?: string; available: boolean }[];
}

/**
 * Screen 108 — a sellable combination, as resolved from the axes chosen on
 * screen 86. `combination` is the same `axisOption|axisOption` key the axes'
 * option ids are built from, in axis order, so a selector can match a pick to
 * a SKU without a second round trip.
 */
export interface ProductSku {
  id: string;
  combination: string;
  price: number;
  stock: number;
  available: boolean;
}

/** Screen 53 — Notifications. */
export type NotificationKind = 'order' | 'offer' | 'account' | 'stock';

export interface Notification {
  id: string;
  kind: NotificationKind;
  title: string;
  body: string;
  createdAt: string;
  read: boolean;
  /** Where tapping the notification goes. */
  href?: string;
}

/** Screen 54 — Support tickets. */
export type TicketStatus = 'open' | 'answered' | 'closed';

export interface SupportTicket {
  id: string;
  subject: string;
  status: TicketStatus;
  /** Latest message in the thread, used as the card preview. */
  lastMessage: string;
  lastMessageFromSupport: boolean;
  updatedAt: string;
}

/** Screens 55 and 56 — Reviews the customer has written. */
export type ReviewStatus = 'published' | 'pending' | 'rejected';

export interface MyReview {
  id: string;
  productSlug: string;
  productTitle: string;
  productImage: string;
  rating: number;
  body: string;
  status: ReviewStatus;
  createdAt: string;
}

/** A delivered item the customer has not reviewed yet — screen 55. */
export interface AwaitingReview {
  orderId: string;
  productSlug: string;
  productTitle: string;
  productImage: string;
  deliveredAt: string;
}

/** Screen 58 — Wallet. */
export interface WalletTransaction {
  id: string;
  title: string;
  /** Positive credits the wallet, negative debits it. */
  amount: number;
  createdAt: string;
  status: 'success' | 'pending' | 'failed';
  icon: string;
}

/** A request to put money into the wallet, and what became of it. */
export interface WalletTopUp {
  id: string;
  amount: number;
  /** `gateway` settles itself; `manual` waits for an operator. */
  method: 'gateway' | 'manual';
  status: 'pending' | 'approved' | 'rejected';
  trackingNumber?: string;
  paidOn?: string;
  /** The operator's note — why it was rejected. */
  reviewNote?: string;
  createdAt: string;
}

/**
 * What screen 58 needs to draw itself.
 *
 * The limits come from the API rather than being repeated here, so the form
 * offers exactly what the server would accept. `manualTopUpEnabled` is false
 * unless the store is staffing the card-to-card review queue — the form is not
 * rendered at all when it is off.
 */
export interface WalletOverview {
  balance: number;
  manualTopUpEnabled: boolean;
  receiptRequired: boolean;
  minimumTopUp: number;
  maximumTopUp: number;
  pendingTopUps: WalletTopUp[];
}

/** Screen 59 — Coupons. */
export interface Coupon {
  id: string;
  code: string;
  title: string;
  condition: string;
  /** Percentage discount, or a fixed Toman amount — one of the two. */
  percent?: number;
  amount?: number;
  expiresAt: string;
  used: boolean;
}

/** Standard envelope for paginated list endpoints. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface ProductQuery {
  category?: string;
  brand?: string;
  search?: string;
  minPrice?: number;
  maxPrice?: number;
  inStockOnly?: boolean;
  sort?: 'newest' | 'bestselling' | 'price-asc' | 'price-desc' | 'rating';
  page?: number;
  pageSize?: number;
}
