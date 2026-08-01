/**
 * B2B requests, quotes and corporate gift bundles.
 * Content transcribed from design screens 20 and 61-70.
 */

import type { B2BRequest, B2BRequestStatus, GiftBundle, Quote } from '../api/types';
import { mockProducts } from './products';

const img = (index: number) => mockProducts[index]?.image ?? mockProducts[0]!.image;

export const b2bStatusMeta: Record<
  B2BRequestStatus,
  { label: string; tone: 'mint' | 'teal' | 'warning' | 'error' | 'neutral'; icon: string }
> = {
  submitted: { label: 'ثبت شده', tone: 'neutral', icon: 'assignment' },
  reviewing: { label: 'در حال بررسی', tone: 'warning', icon: 'hourglass_empty' },
  quoted: { label: 'پیش‌فاکتور آماده', tone: 'mint', icon: 'assignment_turned_in' },
  approved: { label: 'تایید شده', tone: 'teal', icon: 'verified' },
  rejected: { label: 'رد شده', tone: 'error', icon: 'cancel' },
};

/** Audiences listed as chips on screen 20. */
export const b2bAudiences = [
  'مدارس',
  'شرکت‌ها',
  'دانشگاه‌ها',
  'دفاتر معماری',
  'تیم‌های خلاق',
] as const;

/** Request types offered by the quote form on screen 61. */
export const b2bRequestKinds = [
  { id: 'organization', label: 'خرید سازمانی', description: 'تأمین دوره‌ای برای یک سازمان' },
  { id: 'bulk', label: 'سفارش عمده', description: 'خرید تعداد بالا از چند قلم کالا' },
  { id: 'promotional', label: 'هدایای تبلیغاتی', description: 'بسته‌بندی و لوگوی اختصاصی' },
] as const;

/** Organisation types offered on screen 63. */
export const organizationTypes = [
  'شرکت خصوصی',
  'دولتی',
  'مدرسه / آموزشی',
  'سازمان مردم‌نهاد',
  'سایر',
] as const;

function timeline(reached: number) {
  return ['ثبت درخواست', 'بررسی کارشناس', 'صدور پیش‌فاکتور', 'تایید و پرداخت', 'تحویل'].map(
    (label, index) => ({
      id: `b2b-step-${index + 1}`,
      label,
      state: (index < reached ? 'done' : index === reached ? 'current' : 'upcoming') as
        | 'done'
        | 'current'
        | 'upcoming',
    }),
  );
}

export const mockB2BRequests: B2BRequest[] = [
  {
    id: 'req-1',
    code: 'B2B-8902',
    title: 'تجهیزات اداری شعبه ونک',
    kind: 'organization',
    status: 'quoted',
    createdAt: '2026-07-16T00:00:00Z',
    organization: 'شرکت معماری پایدار ایرانیان',
    itemCount: 12,
    quoteId: 'qt-1',
    timeline: timeline(2),
  },
  {
    id: 'req-2',
    code: 'B2B-8895',
    title: 'هدایای پایان سال همکاران',
    kind: 'promotional',
    status: 'reviewing',
    createdAt: '2026-07-09T00:00:00Z',
    organization: 'شرکت معماری پایدار ایرانیان',
    itemCount: 80,
    timeline: timeline(1),
  },
  {
    id: 'req-3',
    code: 'B2B-8871',
    title: 'لوازم‌التحریر شروع سال تحصیلی',
    kind: 'bulk',
    status: 'approved',
    createdAt: '2026-06-24T00:00:00Z',
    organization: 'مجتمع آموزشی اندیشه',
    itemCount: 240,
    quoteId: 'qt-2',
    timeline: timeline(4),
  },
];

export const mockQuotes: Quote[] = [
  {
    id: 'qt-1',
    number: 'QT-1405-8942',
    requestCode: 'B2B-8902',
    organization: 'شرکت معماری پایدار ایرانیان',
    salesRep: 'علیرضا احمدی',
    validUntil: '2026-08-20T00:00:00Z',
    status: 'pending',
    lines: [
      { title: mockProducts[12]!.title, sku: 'BZ-ORG-001', quantity: 4, unitPrice: 950_000 },
      { title: mockProducts[8]!.title, sku: 'BZ-PLN-A5-001', quantity: 12, unitPrice: 350_000 },
      { title: mockProducts[13]!.title, sku: 'BZ-PEN-014', quantity: 24, unitPrice: 680_000 },
    ],
    subtotal: 0,
    discount: 1_200_000,
    tax: 0,
    total: 0,
  },
  {
    id: 'qt-2',
    number: 'QT-1405-8871',
    requestCode: 'B2B-8871',
    organization: 'مجتمع آموزشی اندیشه',
    salesRep: 'مریم کاظمی',
    validUntil: '2026-07-10T00:00:00Z',
    status: 'approved',
    lines: [
      { title: mockProducts[26]!.title, sku: 'BZ-NTB-A5-004', quantity: 120, unitPrice: 245_000 },
      { title: mockProducts[27]!.title, sku: 'BZ-NTB-CLS-002', quantity: 120, unitPrice: 180_000 },
    ],
    subtotal: 0,
    discount: 4_500_000,
    tax: 0,
    total: 0,
  },
];

// Derive money so the lines and the totals can never disagree.
for (const quote of mockQuotes) {
  quote.subtotal = quote.lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
  quote.tax = Math.round((quote.subtotal - quote.discount) * 0.09);
  quote.total = quote.subtotal - quote.discount + quote.tax;
}

/** Screen 66 — Corporate gift bundles. */
export const giftBundleCategories = ['همه بسته‌ها', 'مینیمال', 'هنری', 'مدیریتی'] as const;

export const mockGiftBundles: GiftBundle[] = [
  {
    slug: 'minimal-desk',
    title: 'بسته میز کار مینیمال',
    summary: 'دفتر پلنر، خودنویس و ارگانایزر رومیزی در جعبه کرافت.',
    cover: img(12),
    category: 'مینیمال',
    pricePerUnit: 1_450_000,
    minimumQuantity: 20,
  },
  {
    slug: 'artist-set',
    title: 'بسته هنرمند',
    summary: 'آبرنگ، قلم‌مو و دفتر طراحی برای تیم‌های خلاق.',
    cover: img(0),
    category: 'هنری',
    pricePerUnit: 2_350_000,
    minimumQuantity: 10,
  },
  {
    slug: 'executive',
    title: 'بسته مدیریتی',
    summary: 'خودنویس فلزی، دفترچه چرمی و جعبه چوبی با حکاکی نام.',
    cover: img(15),
    category: 'مدیریتی',
    pricePerUnit: 3_200_000,
    minimumQuantity: 10,
  },
  {
    slug: 'welcome-kit',
    title: 'بسته خوش‌آمدگویی همکار جدید',
    summary: 'ماگ، دفتر، روان‌نویس و کارت دست‌نویس.',
    cover: img(11),
    category: 'مینیمال',
    pricePerUnit: 890_000,
    minimumQuantity: 30,
  },
];

/** Screen 67 — The B2B ordering process. */
export const b2bProcessSteps = [
  {
    title: 'ثبت درخواست',
    body: 'فرم درخواست پیش‌فاکتور را با مشخصات سازمان و اقلام مورد نیاز تکمیل کنید.',
    icon: 'edit_document',
  },
  {
    title: 'تماس کارشناس',
    body: 'کارشناس فروش سازمانی حداکثر تا ۲۴ ساعت کاری با شما تماس می‌گیرد.',
    icon: 'support_agent',
  },
  {
    title: 'صدور پیش‌فاکتور',
    body: 'پیش‌فاکتور رسمی با قیمت‌گذاری سازمانی و شرایط پرداخت برای شما صادر می‌شود.',
    icon: 'receipt_long',
  },
  {
    title: 'تایید و پرداخت',
    body: 'پس از تایید شما، پرداخت به‌صورت نقدی یا اعتباری انجام می‌شود.',
    icon: 'payments',
  },
  {
    title: 'تولید و بسته‌بندی',
    body: 'در صورت سفارشی‌سازی، حکاکی و بسته‌بندی اختصاصی انجام می‌گیرد.',
    icon: 'inventory_2',
  },
  {
    title: 'تحویل',
    body: 'ارسال یکپارچه به آدرس سازمان همراه با فاکتور رسمی.',
    icon: 'local_shipping',
  },
];
