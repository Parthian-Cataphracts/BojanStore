/**
 * Admin fixtures.
 *
 * Stand-ins for the .NET admin endpoints. Content mirrors the design screens
 * 92-160. Delete once `lib/api` points at the real backend.
 */

import type {
  AdminCoupon,
  AdminCustomer,
  AdminOrder,
  AdminProduct,
  AdminUser,
  AuditEntry,
  Campaign,
  ContentEntry,
  ServiceHealth,
  StockMovement,
  SupportThread,
} from './types';
import type { InvoiceDto, InvoiceSummaryDto } from './api/types';

const CDN = 'https://lh3.googleusercontent.com/aida-public/';

/** A handful of real design images, cycled so every row has a thumbnail. */
const images = [
  `${CDN}AB6AXuAAvOHfDPQgaEPUVLchC5qNZPppE5QtfevfXbRf9bmGyFEt32NEeQ-ClLa1PwOns0hgvQ18pOrd8mwfPgN_V5zY5tF738yVKLgPvRWmnu0KORBPJ8vWwMqirlo8V90JynZiSCmvTS21SeRqqRtMPXd-Tks7m4emi6F3lETnbuOGAjRHlqtsjOD6Kx-8dioP272m4wVBSPlCeGJ32HkMW12TXCbj0WPY9izA7_-o8y8qeJnhxZucVQ5y`,
];

const image = (index: number) => images[index % images.length]!;

const products: {
  title: string;
  sku: string;
  brand: string;
  brandSlug: string;
  category: string;
  categorySlug: string;
  price: number;
}[] = [
  { title: 'آبرنگ ۲۴ رنگ حرفه‌ای', sku: 'BZ-ART-WC24-001', brand: 'وینزور اند نیوتون', brandSlug: 'winsor-newton', category: 'ابزار هنری', categorySlug: 'art-tools', price: 1_200_000 },
  { title: 'ست قلم‌مو ۶ عددی دست‌ساز', sku: 'BZ-ART-BRS-006', brand: 'ره‌آورد', brandSlug: 'rahavard', category: 'ابزار هنری', categorySlug: 'art-tools', price: 450_000 },
  { title: 'دفتر طراحی A4 جلد سخت', sku: 'BZ-NTB-A4-120', brand: 'فابریانو', brandSlug: 'fabriano', category: 'دفتر و پلنر', categorySlug: 'notebooks', price: 320_000 },
  { title: 'مداد رنگی پلی‌کروم ۳۶ رنگ', sku: 'BZ-ART-PC36-002', brand: 'فابر کاستل', brandSlug: 'faber-castell', category: 'ابزار هنری', categorySlug: 'art-tools', price: 3_500_000 },
  { title: 'دفتر پلنر مینیمال روزانه', sku: 'BZ-PLN-A5-001', brand: 'بوژان استودیو', brandSlug: 'bojan-studio', category: 'دفتر و پلنر', categorySlug: 'notebooks', price: 350_000 },
  { title: 'ارگانایزر رومیزی', sku: 'BZ-ORG-001', brand: 'بوژان استودیو', brandSlug: 'bojan-studio', category: 'نوشت‌افزار', categorySlug: 'stationery', price: 950_000 },
  { title: 'خودنویس فلزی مینیمال', sku: 'BZ-PEN-FTN-003', brand: 'بوژان استودیو', brandSlug: 'bojan-studio', category: 'نوشت‌افزار', categorySlug: 'stationery', price: 890_000 },
  { title: 'گلدان سفالی دست‌ساز طرح کویر', sku: 'BZ-GFT-VAS-011', brand: 'بوژان استودیو', brandSlug: 'bojan-studio', category: 'هدیه و سبک زندگی', categorySlug: 'gift-lifestyle', price: 2_450_000 },
  { title: 'ماگ سرامیکی ساده', sku: 'BZ-GFT-MUG-004', brand: 'بوژان استودیو', brandSlug: 'bojan-studio', category: 'هدیه و سبک زندگی', categorySlug: 'gift-lifestyle', price: 420_000 },
  { title: 'روان‌نویس کلاسیک', sku: 'BZ-PEN-GEL-014', brand: 'بوژان استودیو', brandSlug: 'bojan-studio', category: 'نوشت‌افزار', categorySlug: 'stationery', price: 680_000 },
];

const stocks = [0, 42, 8, 25, 120, 6, 33, 15, 4, 58];
const statuses: AdminProduct['status'][] = [
  'published', 'published', 'published', 'draft', 'published',
  'published', 'archived', 'published', 'published', 'draft',
];

export const mockAdminProducts: AdminProduct[] = products.map((product, index) => ({
  id: `ap-${index + 1}`,
  ...product,
  costPrice: Math.round(product.price * 0.62),
  stock: stocks[index]!,
  status: statuses[index]!,
  image: image(index),
  updatedAt: new Date(2026, 6, 28 - index).toISOString(),
}));

const customers = [
  'نیلوفر احمدی', 'سینا رستمی', 'مریم کاظمی', 'امیر قاسمی', 'زهرا موسوی',
  'کاوه رحیمی', 'سارا نجفی', 'بهنام صادقی',
];

export const mockAdminCustomers: AdminCustomer[] = customers.map((name, index) => ({
  id: `ac-${index + 1}`,
  name,
  phone: `0912${String(1000000 + index * 111111).slice(0, 7)}`,
  email: index % 3 === 0 ? `customer${index + 1}@example.com` : undefined,
  group: index % 4 === 0 ? 'سازمانی' : index % 3 === 0 ? 'طلایی' : 'عادی',
  orderCount: 3 + index * 2,
  totalSpent: (index + 1) * 2_400_000,
  joinedAt: new Date(2025, index % 12, 5 + index).toISOString(),
  status: index === 7 ? 'blocked' : 'active',
}));

const orderStatuses: AdminOrder['status'][] = [
  'processing', 'shipped', 'delivered', 'pending', 'packed', 'cancelled', 'delivered', 'returned',
];

export const mockAdminOrders: AdminOrder[] = orderStatuses.map((status, index) => {
  const items = products.slice(index % 4, (index % 4) + 2).map((product, i) => ({
    title: product.title,
    sku: product.sku,
    quantity: i + 1,
    unitPrice: product.price,
  }));

  return {
    id: `ao-${index + 1}`,
    number: `BZ-${1024 - index}`,
    customer: customers[index % customers.length]!,
    customerPhone: `0912${String(1000000 + index * 111111).slice(0, 7)}`,
    placedAt: new Date(2026, 6, 29 - index).toISOString(),
    status,
    itemCount: items.length,
    total: items.reduce((sum, item) => sum + item.unitPrice * item.quantity, 0),
    paymentMethod: index % 3 === 0 ? 'کیف پول بوژان' : 'پرداخت اینترنتی',
    shippingMethod: index % 2 === 0 ? 'پست پیشتاز' : 'پیک ویژه بوژان',
    address: 'تهران، خیابان ولیعصر، کوچه فرزان، پلاک ۱۲، واحد ۳',
    items,
  };
});

/**
 * Issued invoices — the delivered orders above, and only those.
 *
 * That is the real rule, not a fixture convenience: the number is minted at the
 * delivery transition, so an order that was cancelled, returned or is still in
 * flight has no invoice. Deriving the list from `mockAdminOrders` rather than
 * writing it out separately is what keeps the mock branch honest about it.
 */
export const mockInvoices: InvoiceSummaryDto[] = mockAdminOrders
  .filter((order) => order.status === 'delivered')
  .map((order, index) => ({
    orderId: order.id,
    invoiceNumber: String(4_819_003_720_551_648 + index * 137).padStart(16, '0'),
    orderNumber: order.number,
    customer: order.customer,
    customerPhone: order.customerPhone,
    // Delivered a few days after it was placed, so the two dates on the
    // document are not suspiciously identical.
    issuedAt: new Date(new Date(order.placedAt).getTime() + 3 * 86_400_000).toISOString(),
    itemCount: order.items.reduce((sum, item) => sum + item.quantity, 0),
    total: order.total,
  }));

/** The full document for one order, or null when that order has no invoice. */
export function mockInvoiceDocument(orderId: string): InvoiceDto | null {
  const summary = mockInvoices.find((invoice) => invoice.orderId === orderId);
  const order = mockAdminOrders.find((candidate) => candidate.id === orderId);
  if (!summary || !order) return null;

  const lines = order.items.map((item, index) => ({
    productId: `p-${index + 1}`,
    productSlug: `product-${index + 1}`,
    title: item.title,
    quantity: item.quantity,
    unitPrice: item.unitPrice,
    lineTotal: item.unitPrice * item.quantity,
  }));

  const subtotal = lines.reduce((sum, line) => sum + line.lineTotal, 0);
  const shipping = 45_000;

  return {
    orderId: order.id,
    invoiceNumber: summary.invoiceNumber,
    orderNumber: order.number,
    placedAt: order.placedAt,
    issuedAt: summary.issuedAt,
    customerName: order.customer,
    customerPhone: order.customerPhone,
    paymentMethod: order.paymentMethod,
    shippingMethod: order.shippingMethod,
    address: order.address,
    lines,
    subtotal,
    couponCode: null,
    discount: 0,
    shipping,
    total: subtotal + shipping,
    returnedCount: 0,
    returnedRefund: 0,
    settings: {
      seller: {
        name: 'فروشگاه بوژان',
        website: 'bojanstore.com',
        email: 'support@bojanstore.com',
        phone: '',
        address: '',
        nationalId: '',
        economicCode: '',
      },
      thanksNote: 'از اعتماد و خرید شما سپاسگزاریم.',
      terms:
        'خریدار با ثبت این سفارش، قوانین و مقررات فروشگاه بوژان را مطالعه کرده و پذیرفته است. مهلت مرجوعی کالا و شرایط گارانتی، مطابق شرایط اعلام‌شده در زمان خرید است.',
      footerNote: 'این فاکتور به‌صورت الکترونیکی صادر شده و بدون مهر و امضای فیزیکی نیز معتبر است.',
      stampUrl: null,
    },
  };
}

export const mockStockMovements: StockMovement[] = [
  { id: 'sm-1', sku: products[0]!.sku, productTitle: products[0]!.title, kind: 'in', quantity: 50, reason: 'ورود از تأمین‌کننده', at: '2026-07-28T09:00:00Z', by: 'مدیر انبار' },
  { id: 'sm-2', sku: products[4]!.sku, productTitle: products[4]!.title, kind: 'out', quantity: 12, reason: 'فروش سفارش BZ-1024', at: '2026-07-27T14:20:00Z', by: 'سیستم' },
  { id: 'sm-3', sku: products[8]!.sku, productTitle: products[8]!.title, kind: 'adjust', quantity: -3, reason: 'آسیب‌دیدگی در انبار', at: '2026-07-26T11:05:00Z', by: 'مدیر انبار' },
  { id: 'sm-4', sku: products[2]!.sku, productTitle: products[2]!.title, kind: 'in', quantity: 80, reason: 'ورود از تأمین‌کننده', at: '2026-07-24T08:30:00Z', by: 'مدیر انبار' },
];

export const mockAdminUsers: AdminUser[] = [
  { id: 'au-1', name: 'مدیر سیستم', email: 'admin@bojan.com', role: 'مالک', lastActiveAt: '2026-07-29T16:40:00Z', status: 'active' },
  { id: 'au-2', name: 'مریم کاظمی', email: 'maryam@bojan.com', role: 'مدیر محصول', lastActiveAt: '2026-07-29T12:10:00Z', status: 'active' },
  { id: 'au-3', name: 'علیرضا احمدی', email: 'alireza@bojan.com', role: 'فروش سازمانی', lastActiveAt: '2026-07-28T17:55:00Z', status: 'active' },
  { id: 'au-4', name: 'سارا نجفی', email: 'sara@bojan.com', role: 'پشتیبانی', lastActiveAt: '2026-07-20T10:00:00Z', status: 'suspended' },
];

export const adminRoles = [
  { id: 'owner', name: 'مالک', description: 'دسترسی کامل به همه بخش‌ها', members: 1 },
  { id: 'product', name: 'مدیر محصول', description: 'کاتالوگ، موجودی و محتوا', members: 1 },
  { id: 'sales', name: 'فروش سازمانی', description: 'درخواست‌ها و پیش‌فاکتورها', members: 1 },
  { id: 'support', name: 'پشتیبانی', description: 'تیکت‌ها و سفارش‌ها (فقط خواندن)', members: 1 },
];

/** Sections a role can be granted; used by the permissions matrix. */
/**
 * The columns of screen 146's grid.
 *
 * `key` is what is stored and what the API enforces on; `label` is only drawn.
 * They used to be the same string, so a permission depended on a Persian label
 * surviving unchanged — rewording a column would have revoked it for everyone
 * who held it. The keys mirror `PanelSection` on the API side.
 */
export const permissionSections = [
  { key: 'orders', label: 'سفارش‌ها' },
  { key: 'products', label: 'محصولات' },
  { key: 'inventory', label: 'موجودی' },
  { key: 'customers', label: 'مشتریان' },
  { key: 'business', label: 'درخواست‌های سازمانی' },
  { key: 'content', label: 'محتوا' },
  { key: 'campaigns', label: 'کمپین‌ها' },
  { key: 'support', label: 'پشتیبانی' },
  { key: 'reports', label: 'گزارش‌ها' },
  { key: 'settings', label: 'تنظیمات' },
] as const;

export const mockAuditLog: AuditEntry[] = [
  { id: 'al-1', actor: 'مریم کاظمی', action: 'ویرایش محصول', target: products[0]!.sku, at: '2026-07-29T15:20:00Z', ip: '192.168.1.24' },
  { id: 'al-2', actor: 'مدیر سیستم', action: 'تغییر وضعیت سفارش', target: 'BZ-1024', at: '2026-07-29T11:02:00Z', ip: '192.168.1.10' },
  { id: 'al-3', actor: 'علیرضا احمدی', action: 'صدور پیش‌فاکتور', target: 'QT-1405-8942', at: '2026-07-28T09:45:00Z', ip: '192.168.1.31' },
  { id: 'al-4', actor: 'مدیر سیستم', action: 'افزودن کاربر ادمین', target: 'sara@bojan.com', at: '2026-07-25T13:15:00Z', ip: '192.168.1.10' },
];

export const mockCampaigns: Campaign[] = [
  { id: 'cp-1', title: 'جشنواره شروع سال تحصیلی', kind: 'discount', status: 'running', startsAt: '2026-07-20T00:00:00Z', endsAt: '2026-08-20T00:00:00Z', reach: 12_400, conversion: 4.8, description: 'تخفیف ویژه لوازم‌التحریر برای شروع سال تحصیلی.' },
  { id: 'cp-2', title: 'بنر هدیه‌های خلاق', kind: 'banner', status: 'scheduled', startsAt: '2026-08-01T00:00:00Z', endsAt: '2026-08-30T00:00:00Z', reach: 0, conversion: 0, description: 'بنر معرفی هدیه‌های خلاق در صفحه اصلی.' },
  { id: 'cp-3', title: 'خبرنامه محصولات جدید', kind: 'email', status: 'ended', startsAt: '2026-06-01T00:00:00Z', endsAt: '2026-06-15T00:00:00Z', reach: 8_900, conversion: 2.1, description: 'ایمیل معرفی محصولات تازه رسیده.' },
  { id: 'cp-4', title: 'پیامک یادآوری سبد رها‌شده', kind: 'sms', status: 'running', startsAt: '2026-07-01T00:00:00Z', endsAt: '2026-09-01T00:00:00Z', reach: 3_200, conversion: 7.4, description: 'پیامک یادآوری برای سبدهای خرید رهاشده.' },
];

export const mockAdminCoupons: AdminCoupon[] = [
  { id: 'acp-1', code: 'WELCOME', title: 'تخفیف اولین خرید', percent: 10, usageLimit: 1000, usedCount: 412, expiresAt: '2026-10-21T00:00:00Z', active: true },
  { id: 'acp-2', code: 'HBD-1405', title: 'هدیه تولد بوژان', amount: 150_000, usageLimit: 500, usedCount: 88, expiresAt: '2026-09-05T00:00:00Z', active: true },
  { id: 'acp-3', code: 'SPRING20', title: 'جشنواره بهاره', percent: 20, usageLimit: 2000, usedCount: 2000, expiresAt: '2026-05-20T00:00:00Z', active: false },
];

export const mockContent: ContentEntry[] = [
  { id: 'ce-1', title: 'چطور یک پلنر مناسب انتخاب کنیم؟', type: 'article', status: 'published', author: 'تیم محتوا', updatedAt: '2026-07-06T00:00:00Z', slug: 'choosing-a-planner', excerpt: 'راهنمای انتخاب پلنر مناسب برای سبک زندگی شما.', body: 'متن کامل مقاله درباره انتخاب پلنر مناسب...', cover: '/images/content/planner.jpg' },
  { id: 'ce-2', title: 'درباره بوژان', type: 'page', status: 'published', author: 'مدیر سیستم', updatedAt: '2026-06-18T00:00:00Z', slug: 'about', body: 'بوژان فروشگاه آنلاین لوازم‌التحریر و هدایای خلاقانه است.' },
  { id: 'ce-3', title: 'بنر صفحه اصلی — شروع سال تحصیلی', type: 'banner', status: 'draft', author: 'تیم محتوا', updatedAt: '2026-07-27T00:00:00Z', slug: 'home-back-to-school', cover: '/images/content/back-to-school.jpg' },
  { id: 'ce-4', title: 'زمان ارسال سفارش چقدر است؟', type: 'faq', status: 'published', author: 'پشتیبانی', updatedAt: '2026-05-30T00:00:00Z', slug: 'shipping-time', body: 'سفارش‌ها معمولا طی ۲ تا ۴ روز کاری ارسال می‌شوند.' },
];

export const mockSupportThreads: SupportThread[] = [
  { id: 'st-1', subject: 'پیگیری سفارش BZ-1024', customer: 'نیلوفر احمدی', status: 'open', priority: 'high', updatedAt: '2026-07-29T15:00:00Z', messageCount: 3 },
  { id: 'st-2', subject: 'سوال درباره ابعاد گلدان سرامیکی', customer: 'سینا رستمی', status: 'answered', priority: 'normal', updatedAt: '2026-07-28T11:20:00Z', messageCount: 2 },
  { id: 'st-3', subject: 'تغییر آدرس ارسال', customer: 'مریم کاظمی', status: 'closed', priority: 'low', updatedAt: '2026-07-04T10:00:00Z', messageCount: 4 },
];

/** Canned replies offered on the support screens. */
export const cannedReplies = [
  { id: 'cr-1', title: 'تأیید دریافت', body: 'سلام، پیام شما دریافت شد. در حال بررسی هستیم و به‌زودی نتیجه را اطلاع می‌دهیم.' },
  { id: 'cr-2', title: 'زمان ارسال', body: 'سفارش شما در حال آماده‌سازی است و طی ۲ تا ۳ روز کاری ارسال می‌شود.' },
  { id: 'cr-3', title: 'راهنمای مرجوعی', body: 'برای ثبت مرجوعی، از بخش سفارش‌های من گزینه «درخواست مرجوعی» را انتخاب کنید.' },
];

export const mockServices: ServiceHealth[] = [
  { id: 'sv-1', name: 'API فروشگاه', status: 'operational', latencyMs: 82, checkedAt: '2026-07-29T16:45:00Z' },
  { id: 'sv-2', name: 'درگاه پرداخت', status: 'operational', latencyMs: 240, checkedAt: '2026-07-29T16:45:00Z' },
  { id: 'sv-3', name: 'سرویس پیامک', status: 'degraded', latencyMs: 1_450, checkedAt: '2026-07-29T16:45:00Z' },
  { id: 'sv-4', name: 'ذخیره‌سازی تصاویر', status: 'operational', latencyMs: 110, checkedAt: '2026-07-29T16:45:00Z' },
];

export const mockCategories = [
  { id: 'cat-1', name: 'نوشت‌افزار', slug: 'stationery', productCount: 42, parent: null },
  { id: 'cat-2', name: 'دفتر و پلنر', slug: 'notebooks', productCount: 69, parent: null },
  { id: 'cat-3', name: 'ابزار هنری', slug: 'art-tools', productCount: 124, parent: null },
  { id: 'cat-4', name: 'هدیه و سبک زندگی', slug: 'gift-lifestyle', productCount: 65, parent: null },
  { id: 'cat-5', name: 'آبرنگ', slug: 'watercolor', productCount: 34, parent: 'ابزار هنری' },
  { id: 'cat-6', name: 'قلم‌مو', slug: 'brushes', productCount: 46, parent: 'ابزار هنری' },
];

export const mockBrands = [
  { id: 'br-1', name: 'بوژان استودیو', slug: 'bojan-studio', productCount: 24 },
  { id: 'br-2', name: 'وینزور اند نیوتون', slug: 'winsor-newton', productCount: 12 },
  { id: 'br-3', name: 'فابر کاستل', slug: 'faber-castell', productCount: 18 },
  { id: 'br-4', name: 'فابریانو', slug: 'fabriano', productCount: 9 },
];

export const mockCollections = [
  { id: 'co-1', name: 'میز کار خلاق', slug: 'creative-desk', productCount: 12, featured: true },
  { id: 'co-2', name: 'شروع سال تحصیلی', slug: 'back-to-school', productCount: 24, featured: false },
  { id: 'co-3', name: 'برای هنرمندان', slug: 'for-artists', productCount: 18, featured: false },
  { id: 'co-4', name: 'هدیه‌های مینیمال', slug: 'minimal-gifts', productCount: 15, featured: true },
];

export const mockB2BAdminRequests = [
  { id: 'br-1', code: 'B2B-8902', organization: 'شرکت معماری پایدار ایرانیان', contact: 'علی رضایی', phone: '09121112233', email: 'ali@payedar.example', itemCount: 12, status: 'quoted' as const, createdAt: '2026-07-16T00:00:00Z' },
  { id: 'br-2', code: 'B2B-8895', organization: 'شرکت معماری پایدار ایرانیان', contact: 'علی رضایی', phone: '09121112233', email: 'ali@payedar.example', itemCount: 80, status: 'reviewing' as const, createdAt: '2026-07-09T00:00:00Z' },
  { id: 'br-3', code: 'B2B-8871', organization: 'مجتمع آموزشی اندیشه', contact: 'مریم کاظمی', phone: '09124445566', email: 'maryam@andisheh.example', itemCount: 240, status: 'approved' as const, createdAt: '2026-06-24T00:00:00Z' },
];
