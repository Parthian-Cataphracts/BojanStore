/**
 * Mock order data, matching the content drawn on screens 12 and 13.
 * Removed together with the rest of `lib/mock` once the API is live.
 */

import type { Invoice, OrderDetail, OrderStatus } from '../api/types';
import { mockProducts } from './products';

/** Persian labels and badge tones for each status, shared by every order view. */
export const orderStatusMeta: Record<
  OrderStatus,
  { label: string; tone: 'mint' | 'teal' | 'warning' | 'error' | 'neutral' }
> = {
  pending: { label: 'در انتظار پرداخت', tone: 'warning' },
  processing: { label: 'در حال آماده‌سازی', tone: 'teal' },
  shipped: { label: 'ارسال شده', tone: 'teal' },
  delivered: { label: 'تحویل شده', tone: 'mint' },
  cancelled: { label: 'لغو شده', tone: 'error' },
  returned: { label: 'مرجوع شده', tone: 'neutral' },
};

/** The five fulfilment steps from screen 13, cut off at the order's progress. */
function timeline(reached: number) {
  return ['ثبت سفارش', 'تأیید پرداخت', 'آماده‌سازی', 'ارسال', 'تحویل'].map((label, index) => ({
    id: `step-${index + 1}`,
    label,
    state: (index < reached ? 'done' : index === reached ? 'current' : 'upcoming') as
      | 'done'
      | 'current'
      | 'upcoming',
  }));
}

function itemsFrom(indexes: number[], quantities: number[]) {
  return indexes.map((productIndex, i) => {
    const product = mockProducts[productIndex]!;
    return {
      productId: product.id,
      slug: product.slug,
      title: product.title,
      image: product.image,
      quantity: quantities[i] ?? 1,
      unitPrice: product.price,
    };
  });
}

function totals(items: { quantity: number; unitPrice: number }[], discount: number, shipping: number) {
  const subtotal = items.reduce((sum, item) => sum + item.unitPrice * item.quantity, 0);
  return { subtotal, discount, shipping, total: subtotal - discount + shipping };
}

const order1Items = itemsFrom([8, 13, 11], [1, 2, 1]);
const order2Items = itemsFrom([15], [1]);
const order3Items = itemsFrom([0, 1, 2, 3, 5], [1, 1, 2, 1, 1]);
const order4Items = itemsFrom([9, 12], [1, 1]);

export const mockOrderDetails: OrderDetail[] = [
  {
    id: 'o-1',
    number: 'BZ-1024',
    placedAt: '2026-07-15T10:24:00Z',
    status: 'processing',
    itemCount: order1Items.length,
    ...totals(order1Items, 50_000, 35_000),
    items: order1Items,
    thumbnails: order1Items.map((item) => item.image),
    timeline: timeline(2),
    shippingAddress: 'تهران، خیابان ولیعصر، نرسیده به میدان ونک، کوچه فرزان، پلاک ۱۲، واحد ۳',
    shippingMethod: 'پست پیشتاز (۲ تا ۳ روز کاری)',
    paymentMethod: 'پرداخت اینترنتی (درگاه سامان)',
  },
  {
    id: 'o-2',
    number: 'BZ-1018',
    placedAt: '2026-07-03T14:02:00Z',
    status: 'delivered',
    itemCount: order2Items.length,
    ...totals(order2Items, 0, 45_000),
    items: order2Items,
    thumbnails: order2Items.map((item) => item.image),
    timeline: timeline(5),
    shippingAddress: 'تهران، میدان ونک، خیابان ملاصدرا، ساختمان تجاری، طبقه ۴',
    shippingMethod: 'تیپاکس (۲ تا ۳ روز کاری)',
    paymentMethod: 'کیف پول بوژان',
    trackingCode: '۲۴۵۹۸۷۳۱۰۰۲۴',
  },
  {
    id: 'o-3',
    number: 'BZ-1007',
    placedAt: '2026-06-21T09:15:00Z',
    status: 'shipped',
    itemCount: order3Items.length,
    ...totals(order3Items, 220_000, 45_000),
    items: order3Items,
    thumbnails: order3Items.map((item) => item.image),
    timeline: timeline(3),
    shippingAddress: 'تهران، خیابان ولیعصر، کوچه بهار، پلاک ۱۲، واحد ۳',
    shippingMethod: 'پست پیشتاز (۲ تا ۳ روز کاری)',
    paymentMethod: 'پرداخت اینترنتی (درگاه سامان)',
    trackingCode: '۲۴۵۹۸۷۳۱۰۰۰۷',
  },
  {
    id: 'o-4',
    number: 'BZ-0996',
    placedAt: '2026-06-08T18:40:00Z',
    status: 'cancelled',
    itemCount: order4Items.length,
    ...totals(order4Items, 0, 0),
    items: order4Items,
    thumbnails: order4Items.map((item) => item.image),
    timeline: timeline(1),
    shippingAddress: 'تهران، خیابان ولیعصر، کوچه بهار، پلاک ۱۲، واحد ۳',
    shippingMethod: 'پیک سریع (تهران)',
    paymentMethod: 'پرداخت اینترنتی (درگاه سامان)',
  },
];

/**
 * The invoice for a mock order, or null when that order has none.
 *
 * Only the delivered one has an invoice, and that is the real rule rather than
 * a fixture shortcut: the number is issued at the delivery transition, so an
 * order still in flight or cancelled has nothing to bill. Screen 34 reached
 * from any other order shows its not-found state, in mock mode exactly as
 * against the API.
 */
export function mockInvoice(idOrNumber: string): Invoice | null {
  const order = mockOrderDetails.find(
    (candidate) => candidate.id === idOrNumber || candidate.number === idOrNumber,
  );
  if (!order || order.status !== 'delivered') return null;

  return {
    orderId: order.id,
    invoiceNumber: '4819003720551648',
    orderNumber: order.number,
    placedAt: order.placedAt,
    // Delivered three days after it was placed — the two dates on the document
    // should not be suspiciously identical.
    issuedAt: new Date(new Date(order.placedAt).getTime() + 3 * 86_400_000).toISOString(),
    // `mockUser`'s own name and number, written out rather than imported:
    // `catalog.ts` already imports this module, and importing it back would
    // close a cycle for two strings.
    customerName: 'نیلوفر احمدی',
    customerPhone: '09121234567',
    paymentMethod: order.paymentMethod,
    shippingMethod: order.shippingMethod,
    address: order.shippingAddress,
    lines: order.items.map((item) => ({
      productId: item.productId,
      productSlug: item.slug,
      title: item.title,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      lineTotal: item.unitPrice * item.quantity,
    })),
    subtotal: order.subtotal,
    couponCode: null,
    discount: order.discount,
    shipping: order.shipping,
    total: order.total,
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
