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

export interface AdminCustomerDto {
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

export interface AdminUserDto {
  id: string;
  name: string;
  email: string;
  role: string;
  lastActiveAt?: string | null;
  status: string;
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
