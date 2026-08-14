/**
 * DTOs returned by `/api/admin/**`, mirroring
 * `backend/src/Bojan.Application/Contracts/AdminContracts.cs` field-for-field
 * (camelCase, as .NET's default JSON serializer emits them).
 *
 * Deliberately separate from `lib/types.ts`, which shapes the mock fixtures —
 * the two will converge once `lib/mock.ts` is retired.
 */

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AdminOrderItem {
  title: string;
  sku: string;
  quantity: number;
  unitPrice: number;
}

export interface AdminOrderDto {
  id: string;
  number: string;
  customer: string;
  customerPhone: string;
  placedAt: string;
  status: string;
  itemCount: number;
  total: number;
  paymentMethod: string;
  shippingMethod: string;
  address: string;
  items: AdminOrderItem[];
  /**
   * The delivery window the shopper asked for on screen 74. A preference
   * nothing schedules against, but the operator packing the order needs to see
   * it — before this it was collected and discarded.
   */
  deliveryWindow?: string;
  /**
   * Whether the money for this order was actually collected —
   * `awaiting-payment`, `paid`, `refunded` or `failed`.
   *
   * Optional only so the type still describes a response from an API that
   * predates it. It was missing from this DTO entirely, which is why the panel
   * had no way to show whether an order had been paid for and no control to
   * record that it had.
   */
  paymentStatus?: string;
  paidAt?: string | null;
  paymentReference?: string | null;
}

export interface InvoiceLineDto {
  productId: string;
  productSlug: string;
  title: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

/**
 * The invoice document, shared with the storefront's screen 34 — the copy the
 * shop keeps and the copy the buyer holds are the same payload, so the two can
 * never disagree. See `InvoiceContracts.cs`.
 */
export interface InvoiceDto {
  orderId: string;
  invoiceNumber: string;
  orderNumber: string;
  placedAt: string;
  /** When the order was delivered, which is when the number was issued. */
  issuedAt: string;
  customerName: string;
  customerPhone: string;
  paymentMethod: string;
  shippingMethod: string;
  address: string;
  lines: InvoiceLineDto[];
  subtotal: number;
  couponCode?: string | null;
  discount: number;
  shipping: number;
  total: number;
  /** Units returned and refunded — reported once, never itemised on the bill. */
  returnedCount: number;
  returnedRefund: number;
  settings: InvoiceSettingsDto;
}

/** Who the invoice says is selling — set on the invoice settings screen. */
export interface InvoiceSellerDto {
  name: string;
  website: string;
  email: string;
  phone: string;
  address: string;
  nationalId: string;
  economicCode: string;
}

/**
 * The shop's own words on the invoice, as the owner configured them.
 *
 * Carried on the invoice rather than fetched beside it, so the customer's copy
 * and the panel's are rendered from the same values — see `InvoiceSettings.cs`.
 */
export interface InvoiceSettingsDto {
  seller: InvoiceSellerDto;
  thanksNote: string;
  terms: string;
  footerNote: string;
  /**
   * The uploaded stamp.
   *
   * Optional because the API omits nulls entirely (`WhenWritingNull`), so an
   * unset stamp arrives as a missing key rather than an explicit null — and the
   * document draws the empty box to stamp by hand instead.
   */
  stampUrl?: string | null;
}

/** One row of the invoice list; lighter than the document itself. */
export interface InvoiceSummaryDto {
  orderId: string;
  invoiceNumber: string;
  orderNumber: string;
  customer: string;
  customerPhone: string;
  issuedAt: string;
  itemCount: number;
  total: number;
}

export interface AdminProductDto {
  id: string;
  sku: string;
  title: string;
  brand: string;
  /** What `POST /products` expects back in its own `brand` field. */
  brandSlug: string;
  category: string;
  categorySlug: string;
  price: number;
  costPrice: number;
  stock: number;
  status: string;
  image: string;
  /**
   * The whole gallery, primary image first — what `POST /products` reads back
   * in its own `images` field. Absent on the list projection, which has no
   * reason to load a gallery per row.
   */
  images?: string[];
  updatedAt: string;
  /**
   * The rest of what the product form posts, read back so editing a product
   * shows what is stored instead of an empty field over a saved value.
   * Absent on the list projection, like `images`.
   */
  slug?: string;
  compareAt?: number | null;
  lowStock?: number;
  trackStock?: boolean;
  backorder?: boolean;
  metaTitle?: string | null;
  metaDescription?: string | null;
  description?: string | null;
}

// --- product detail screens (106, 107, 108) --------------------------------

export interface AdminVariantOptionDto {
  /** Latin, lowercase — what a SKU's combination is built from. */
  key: string;
  label: string;
  /** Present on a swatch axis, absent on a chip. */
  hex?: string;
  available: boolean;
}

export interface AdminVariantAxisDto {
  key: string;
  label: string;
  kind: 'swatch' | 'chip';
  options: AdminVariantOptionDto[];
}

export interface AdminSkuDto {
  id: string;
  code: string;
  barcode?: string;
  /** Option keys, one per axis, joined by `|` — e.g. `cream|a5`. */
  combination: string;
  price: number;
  stock: number;
  active: boolean;
}

export interface AdminAttributeDto {
  id: string;
  name: string;
  kind: 'text' | 'number' | 'boolean';
  values: string[];
  filterable: boolean;
}

/**
 * One rung of a product's B2B volume ladder — "from twenty units, ten percent".
 *
 * Floors rather than ranges: a gap between two ranges is a quantity with no
 * price, and a floor cannot leave one.
 */
export interface ProductVolumeTierDto {
  minimumQuantity: number;
  discountPercent: number;
}

/**
 * A product as the quote composer offers it, ladder attached.
 *
 * The ladder travels with the price so the screen can show a rep what a hundred
 * units come to before they issue anything. Everything is priced again on the
 * server when the quote is submitted — this is what the operator sees, never
 * what they are trusted to send back.
 */
export interface AdminQuotableProductDto {
  id: string;
  title: string;
  sku: string;
  price: number;
  tiers: ProductVolumeTierDto[];
}

/**
 * The shop's Web Push identity, as the settings screen sees it.
 *
 * `publicKey` is readable on purpose — it is what browsers subscribe against
 * and is published material. The private half never leaves the server in either
 * direction; `hasPrivateKey` is all the screen is told about it.
 */
export interface WebPushSettingsDto {
  enabled: boolean;
  publicKey: string;
  hasPrivateKey: boolean;
  subject: string;
}

export interface CatalogueOptionDto {
  slug: string;
  name: string;
}

export interface AdminCategoryDto {
  id: string;
  name: string;
  slug: string;
  icon: string;
  image?: string | null;
  parentId?: string | null;
  parentName?: string | null;
  productCount: number;
  status: string;
  metaTitle?: string | null;
  metaDescription?: string | null;
  showInMenu?: boolean;
  order?: number;
}

export interface AdminBrandDto {
  id: string;
  name: string;
  slug: string;
  tagline?: string | null;
  description?: string | null;
  logo?: string | null;
  cover?: string | null;
  featured: boolean;
  productCount: number;
  status: string;
  country?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
}

export interface AdminCollectionDto {
  id: string;
  title: string;
  slug: string;
  summary?: string | null;
  cover?: string | null;
  editorialNote?: string | null;
  featured: boolean;
  productCount: number;
  status: string;
}

export interface AdminCustomerDto extends AdminCustomerEditFields {
  id: string;
  name: string;
  phone: string;
  email?: string | null;
  group: string;
  orderCount: number;
  totalSpent: number;
  joinedAt: string;
  status: string;
}

export interface StockMovementDto {
  id: string;
  sku: string;
  productTitle: string;
  kind: string;
  quantity: number;
  reason: string;
  at: string;
  by: string;
}

/** One row of the inventory list — stock plus the thresholds screen 107 colours by. */
export interface InventoryRowDto {
  id: string;
  sku: string;
  title: string;
  category: string;
  stock: number;
  lowStockThreshold: number;
  updatedAt: string;
}

/**
 * One magazine article as the panel lists and edits it.
 *
 * Distinct from `ContentEntryDto`: the tables are distinct, and only this one
 * is the magazine. `body` is filled by the detail endpoint only.
 */
/**
 * One account of any kind for the panel's single users list.
 *
 * Shoppers and operators live in separate tables and always will — one is named
 * by orders, the other by audit entries. What the panel needed was one *list*,
 * because "who has an account here" is a single question, and answering it from
 * two screens meant an operator who also shops appeared twice with nothing to
 * say the two were the same person.
 */
export interface AdminAccountDto {
  id: string;
  name: string;
  phone: string;
  email?: string | null;
  /** Which table, and so which editor opens. */
  kind: 'customer' | 'operator';
  role: 'customer' | 'owner' | 'product' | 'sales' | 'support';
  status: 'active' | 'blocked' | 'suspended';
  joinedAt: string;
  /** The shop's reference for a shopper; empty for an operator. */
  code?: string;
  /** Set on an operator who also shops here — the customer row they order through. */
  linkedCustomerId?: string | null;
}

/** Extra fields the customer detail read fills; the list leaves them undefined. */
export interface AdminCustomerEditFields {
  /** The shop's own reference — `BZ-00042`. */
  code?: string;
  firstName?: string | null;
  lastName?: string | null;
  city?: string | null;
  nationalId?: string | null;
  /** ISO `YYYY-MM-DD`. */
  birthDate?: string | null;
  /** False when the account has traded and must be suspended rather than removed. */
  deletable?: boolean;
}

export interface AdminArticleDto {
  id: string;
  slug: string;
  title: string;
  excerpt: string;
  category: string;
  cover: string;
  status: 'published' | 'draft' | 'archived';
  featured: boolean;
  readingMinutes: number;
  publishedAt: string;
  body?: string;
}

export interface AdminUserDto {
  id: string;
  name: string;
  email: string;
  role: string;
  lastActiveAt?: string | null;
  status: string;
  /** The other sign-in identity. Optional — an operator may have only an email. */
  phone?: string | null;
  /** Whether the row's "clear second factor" rescue has anything to clear. */
  twoFactorEnabled?: boolean;
  /** Still carrying a password their owner typed, and held on the change screen until they replace it. */
  mustChangePassword?: boolean;
  createdAt?: string;
}

export interface AuditEntryDto {
  id: string;
  actor: string;
  action: string;
  target: string;
  at: string;
  ip: string;
}

export interface CampaignDto {
  id: string;
  title: string;
  kind: string;
  status: string;
  startsAt?: string | null;
  endsAt?: string | null;
  reach: number;
  conversion: number;
  /** Only populated by `getCampaign` — the list screen has no use for it. */
  description?: string | null;
}

export interface AdminCouponDto {
  id: string;
  code: string;
  title: string;
  percent?: number | null;
  amount?: number | null;
  usageLimit: number;
  usedCount: number;
  expiresAt?: string | null;
  active: boolean;
  /** Only populated by the single-record read, not the list. */
  minimumSpend?: number | null;
}

export interface ContentEntryDto {
  id: string;
  title: string;
  type: string;
  status: string;
  author: string;
  updatedAt: string;
  /** Only populated by `getContentEntry` — the list screen has no use for these. */
  slug?: string | null;
  excerpt?: string | null;
  body?: string | null;
  cover?: string | null;
}

export interface SupportThreadDto {
  id: string;
  subject: string;
  customer: string;
  status: string;
  priority: string;
  updatedAt: string;
  messageCount: number;
}

export interface SupportThreadMessageDto {
  id: string;
  body: string;
  fromSupport: boolean;
  sentAt: string;
}

export interface SupportThreadDetailDto {
  id: string;
  subject: string;
  customer: string;
  customerPhone: string;
  customerEmail?: string | null;
  status: string;
  priority: string;
  updatedAt: string;
  messages: SupportThreadMessageDto[];
}

export interface CannedReplyDto {
  id: string;
  title: string;
  body: string;
  updatedAt: string;
}

export interface LiveChatMessageDto {
  id: string;
  fromSupport: boolean;
  body: string;
  sentAtUtc: string;
}

/** One row of the live-chat conversation list — the storefront widget's threads. */
export interface LiveChatConversationDto {
  visitorId: string;
  lastMessage: string;
  lastMessageAt: string;
  lastFromSupport: boolean;
  unreadCount: number;
}

export interface AdminBusinessRequestDto {
  id: string;
  code: string;
  title: string;
  kind: string;
  status: string;
  organization: string;
  contact: string;
  phone: string;
  email?: string | null;
  itemCount: number;
  assigneeId?: string | null;
  note?: string | null;
  createdAt: string;
}

export interface ApiKeyDto {
  id: string;
  label: string;
  prefix: string;
  scope: string;
  revoked: boolean;
  createdAt: string;
  lastUsedAt?: string | null;
}

/** Returned once, at creation. The plaintext key never appears again. */
export interface CreatedApiKeyDto {
  id: string;
  label: string;
  prefix: string;
  scope: string;
  key: string;
}

/** Screen 146 — one granted cell of the role×section grid. */
export interface RolePermissionDto {
  role: string;
  section: string;
}

/**
 * Screen 156 — one queued or completed backup job.
 *
 * `downloadable`, not a location: the archive is never at a URL this panel
 * could link to directly — see `IBackupArchiver` on the backend.
 */
export interface BackupJobDto {
  id: string;
  kind: string;
  status: 'queued' | 'running' | 'completed' | 'failed';
  downloadable: boolean;
  sizeBytes: number | null;
  error: string | null;
  requestedAt: string;
  completedAt: string | null;
}

export interface ServiceHealthDto {
  id: string;
  name: string;
  /** One of the three `healthMeta` renders. */
  status: 'operational' | 'degraded' | 'down';
  latencyMs: number;
  checkedAt: string;
  /** What failed, when something did. Absent for a healthy check. */
  detail?: string;
}

/** Dashboard's server-status card — the process and host the API runs on. */
export interface ServerStatusDto {
  environment: string;
  dotnetVersion: string;
  operatingSystem: string;
  uptimeSeconds: number;
  workingSetBytes: number;
  threadCount: number;
  processorCount: number;
  cpuLoadPercent?: number;
  totalDiskBytes?: number;
  freeDiskBytes?: number;
  databaseHealthy: boolean;
}

// ---------------------------------------------------------------------------
// Dashboard and reports — screens 92 and 133-140.
// ---------------------------------------------------------------------------

export interface DashboardKpisDto {
  revenueToday: number;
  revenueThisMonth: number;
  ordersToday: number;
  ordersThisMonth: number;
  pendingOrders: number;
  lowStockProducts: number;
  newCustomersThisMonth: number;
  openSupportThreads: number;
}

export interface SalesPointDto {
  period: string;
  revenue: number;
  orders: number;
}

export interface StatusCountDto {
  status: string;
  count: number;
}

export interface TopProductDto {
  productId: string;
  title: string;
  sku: string;
  unitsSold: number;
  revenue: number;
}

export interface CustomerGrowthPointDto {
  period: string;
  newCustomers: number;
  returningCustomers: number;
}

export interface CampaignPerformanceDto {
  campaignId: string;
  title: string;
  reach: number;
  conversion: number;
  conversionRate: number;
}

export interface PaymentMethodTotalDto {
  method: string;
  count: number;
  amount: number;
}

export interface FinancialTotalsDto {
  grossRevenue: number;
  discounts: number;
  shipping: number;
  netRevenue: number;
  costOfGoods: number;
  grossProfit: number;
  orderCount: number;
  /**
   * The same revenue split by how it was paid, aggregated over every order in
   * the range — so this sums to `netRevenue` rather than to whatever fitted on
   * one page.
   */
  byPaymentMethod?: PaymentMethodTotalDto[];
}

export interface StockLevelsDto {
  inStock: number;
  lowStock: number;
  outOfStock: number;
  inventoryValue: number;
  /** Units on hand across the catalogue, counted in the database. */
  totalUnits: number;
}

/** Screen 137 — catalogue counts by state, counted in the database. */
export interface CatalogueSummaryDto {
  total: number;
  published: number;
  draft: number;
  archived: number;
  outOfStock: number;
}

/** Screen 138 — customer-base totals, counted in the database. */
export interface CustomerSummaryDto {
  total: number;
  business: number;
  blocked: number;
  totalSpend: number;
}

/** Generic settings section payload — shape varies by `section`. */
export type SettingsSectionDto = Record<string, unknown>;

// --- support mailbox --------------------------------------------------------

export interface MailAddressDto {
  name: string;
  address: string;
}

export interface MailAttachmentDto {
  index: number;
  fileName: string;
  contentType: string;
  size: number;
}

export interface MailThreadMessageDto {
  folder: string;
  uid: number;
  /** True for something the customer sent, false for one of our replies. */
  fromCustomer: boolean;
  from: MailAddressDto;
  to: MailAddressDto[];
  date: string;
  textBody: string;
  /** Sanitized on the server; still rendered inside a sandboxed frame. */
  htmlBody: string;
  /** Sanitizing removed remote images, so the screen can say they were blocked. */
  hadRemoteContent: boolean;
  seen: boolean;
  attachments: MailAttachmentDto[];
}

export interface MailConversationSummaryDto {
  id: string;
  subject: string;
  /** The outside participant — the customer, never the support address. */
  party: MailAddressDto;
  lastDate: string;
  count: number;
  unread: number;
  preview: string;
  hasAttachments: boolean;
  /** False when the last message was ours, which reads as "waiting on them". */
  lastFromCustomer: boolean;
}

export interface MailConversationPageDto {
  items: MailConversationSummaryDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface MailConversationDetailDto {
  id: string;
  subject: string;
  party: MailAddressDto;
  /** Where the newest inbound message lives, so a reply threads onto it. */
  replyFolder?: string | null;
  replyUid?: number | null;
  messages: MailThreadMessageDto[];
}

/** Never carries the password — it is write-only from the panel's side. */
export interface MailboxSettingsDto {
  enabled: boolean;
  imapHost: string;
  imapPort: number;
  imapUseSsl: boolean;
  smtpHost: string;
  smtpPort: number;
  smtpUseSsl: boolean;
  username: string;
  /** Whether one is stored, so the form can say "saved" over an empty box. */
  hasPassword: boolean;
  address: string;
  displayName: string;
}

/**
 * The payment gateway, as the panel may see it.
 *
 * Never carries the merchant id: it is the credential that lets money be
 * requested in this shop's name, so it is write-only from the panel's side the
 * same way the mailbox password is. `hasMerchantId` is how the form knows to
 * say "ثبت شده" over an empty box.
 */
export interface PaymentGatewaySettingsDto {
  /** `none`, `sandbox` or `zarinpal`. */
  provider: string;
  useSandboxEndpoints: boolean;
  hasMerchantId: boolean;
  callbackUrl: string;
  description: string;
}

/**
 * Which payment options the checkout offers.
 *
 * These are the shop's `PaymentMethod` rows rather than settings keys, which is
 * what makes the switches switch something — before, they wrote into a table
 * nothing read.
 */
export interface PaymentMethodSwitchesDto {
  online: boolean;
  wallet: boolean;
  cashOnDelivery: boolean;
}

export interface PaymentSettingsDto {
  gateway: PaymentGatewaySettingsDto;
  methods: PaymentMethodSwitchesDto;
}

/** Never carries the API key — write-only, like the merchant id and the mailbox password. */
export interface SmsSettingsDto {
  /** `none` or `smsir`. */
  provider: string;
  hasApiKey: boolean;
  /** The advertising line campaigns go out on. */
  lineNumber: string;
  /** The service-line template a sign-in code is sent through. */
  otpTemplateId: number;
  /** The placeholder inside that template the code is substituted into. */
  otpParameterName: string;
}

/**
 * One shipping tier as the panel edits it.
 *
 * `code` is the wire id the checkout submits and the one field the panel cannot
 * change — the checkout screens name these tiers as constants, so renaming one
 * would leave a shopper submitting an id the shop no longer has.
 */
export interface AdminShippingMethodDto {
  code: string;
  title: string;
  /** In Toman, like every other figure in this system. */
  price: number;
  estimate: string;
  isActive: boolean;
  /**
   * What the goods have to come to for this method to cost nothing.
   *
   * Null is always charged, zero is always free, and anything else is free at or
   * above that amount. Per method rather than one figure for the shop, because a
   * courier that is never free and a post tier that is free over a million are
   * both ordinary and one shop wants both at once.
   */
  freeAboveAmount?: number | null;
}

/** What a "test this configuration" button gets back. */
export interface ProviderTestResult {
  ok: boolean;
  error?: string | null;
  detail?: string | null;
}

/** One product line inside a return request. */
export interface AdminReturnItemDto {
  productId: string;
  slug: string;
  title: string;
  image: string;
  quantity: number;
  /** Priced from the order's own frozen line price, never from the catalogue. */
  unitPrice: number;
}

export type AdminReturnStatus =
  | 'submitted'
  | 'reviewing'
  | 'approved'
  | 'received'
  | 'refunded'
  | 'rejected';

/** A return request in the operator's queue — `GET /admin/returns`. */
export interface AdminReturnDto {
  id: string;
  code: string;
  orderId: string;
  orderNumber: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  status: AdminReturnStatus;
  reason: string;
  description?: string | null;
  refundMethod: string;
  /**
   * What refunding would pay, worked out by the server from the order's frozen
   * prices. Shown before the operator commits, so the number on the button is
   * the number that will move.
   */
  refundEstimate: number;
  /** What was actually paid back. Zero until the request reaches `refunded`. */
  refundAmount: number;
  /**
   * False when the order was never actually paid for — a delivered
   * cash-on-delivery order nobody settled. Refunding one would pay out money
   * the shop never took.
   */
  payable: boolean;
  restocked: boolean;
  reviewNote?: string | null;
  createdAt: string;
  refundedAt?: string | null;
  items: AdminReturnItemDto[];
}

/**
 * One rung of the loyalty club.
 *
 * The club used to be a page and nothing else — three tiers advertised, a
 * permanent discount promised, and nothing anywhere that applied any of it.
 * These are rows now, and the checkout prices from them.
 */
export interface LoyaltyTierDto {
  name: string;
  minimumPoints: number;
  discountPercent: number;
  /** Members of this tier are never charged for delivery, whatever method they pick. */
  freeShipping: boolean;
  sortOrder?: number;
}

export interface LoyaltyProgrammeDto {
  /** False when no tier is configured, which is how a shop says it runs no club. */
  enabled: boolean;
  /** Toman a member spends to earn one point. Zero pauses the club without deleting balances. */
  tomanPerPoint: number;
  tiers: LoyaltyTierDto[];
}
