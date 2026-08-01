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
  updatedAt: string;
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

export interface ServiceHealthDto {
  id: string;
  name: string;
  status: string;
  latencyMs: number;
  checkedAt: string;
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

export interface FinancialTotalsDto {
  grossRevenue: number;
  discounts: number;
  shipping: number;
  netRevenue: number;
  costOfGoods: number;
  grossProfit: number;
  orderCount: number;
}

export interface StockLevelsDto {
  inStock: number;
  lowStock: number;
  outOfStock: number;
  inventoryValue: number;
}

/** Generic settings section payload — shape varies by `section`. */
export type SettingsSectionDto = Record<string, unknown>;
