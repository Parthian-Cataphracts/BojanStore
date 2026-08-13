/**
 * Contracts for the admin API.
 *
 * Deliberately separate from the storefront's `types.ts`: the admin sees fields
 * a customer never does (cost price, stock movements, audit trails), and the
 * two will be served by different .NET controllers.
 */

export type AdminOrderStatus =
  | 'pending'
  | 'processing'
  | 'packed'
  | 'shipped'
  | 'delivered'
  | 'cancelled'
  | 'returned';

export interface AdminOrder {
  id: string;
  number: string;
  customer: string;
  customerPhone: string;
  placedAt: string;
  status: AdminOrderStatus;
  itemCount: number;
  total: number;
  paymentMethod: string;
  shippingMethod: string;
  address: string;
  items: { title: string; sku: string; quantity: number; unitPrice: number }[];
}

export interface AdminProduct {
  id: string;
  sku: string;
  title: string;
  brand: string;
  brandSlug: string;
  category: string;
  categorySlug: string;
  price: number;
  costPrice: number;
  stock: number;
  status: 'published' | 'draft' | 'archived';
  image: string;
  updatedAt: string;
}

export interface AdminCustomer {
  id: string;
  name: string;
  phone: string;
  email?: string;
  group: string;
  orderCount: number;
  totalSpent: number;
  joinedAt: string;
  status: 'active' | 'blocked';
}

export interface StockMovement {
  id: string;
  sku: string;
  productTitle: string;
  kind: 'in' | 'out' | 'adjust';
  quantity: number;
  reason: string;
  at: string;
  by: string;
}

export interface AdminUser {
  id: string;
  name: string;
  email: string;
  /** The API's own machine key — 'owner' | 'product' | 'sales' | 'support'. */
  role: string;
  lastActiveAt: string;
  status: 'active' | 'suspended';
  phone?: string;
  twoFactorEnabled?: boolean;
  mustChangePassword?: boolean;
}

export interface AuditEntry {
  id: string;
  actor: string;
  action: string;
  target: string;
  at: string;
  ip: string;
}

export interface Campaign {
  id: string;
  title: string;
  kind: 'discount' | 'banner' | 'email' | 'sms';
  status: 'scheduled' | 'running' | 'ended';
  startsAt: string;
  endsAt: string;
  reach: number;
  conversion: number;
  description?: string;
}

export interface AdminCoupon {
  id: string;
  code: string;
  title: string;
  percent?: number;
  amount?: number;
  usageLimit: number;
  usedCount: number;
  expiresAt: string;
  active: boolean;
}

export interface ContentEntry {
  id: string;
  title: string;
  type: 'article' | 'page' | 'banner' | 'faq';
  status: 'published' | 'draft';
  author: string;
  updatedAt: string;
  slug?: string;
  excerpt?: string;
  body?: string;
  cover?: string;
}

export interface SupportThread {
  id: string;
  subject: string;
  customer: string;
  status: 'open' | 'answered' | 'closed';
  priority: 'low' | 'normal' | 'high';
  updatedAt: string;
  messageCount: number;
}

export interface ServiceHealth {
  id: string;
  name: string;
  status: 'operational' | 'degraded' | 'down';
  latencyMs: number;
  checkedAt: string;
}
