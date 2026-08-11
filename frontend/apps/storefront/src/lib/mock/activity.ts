/**
 * Notifications, support tickets, reviews, wallet history and coupons.
 * Content transcribed from design screens 53-59.
 */

import type {
  AwaitingReview,
  Coupon,
  MyReview,
  Notification,
  SupportTicket,
  WalletTransaction,
} from '../api/types';
import { mockOrderDetails } from './orders';
import { mockProducts } from './products';

const product = (index: number) => mockProducts[index]!;

/** Screen 53 — Notifications. */
export const notificationKindMeta = {
  order: { label: 'سفارش‌ها', icon: 'local_mall' },
  offer: { label: 'پیشنهادها', icon: 'sell' },
  account: { label: 'حساب کاربری', icon: 'manage_accounts' },
  stock: { label: 'موجودی کالا', icon: 'inventory_2' },
  business: { label: 'خرید سازمانی', icon: 'business_center' },
} as const;

export const mockNotifications: Notification[] = [
  {
    id: 'n-1',
    kind: 'order',
    title: 'سفارش شما در حال آماده‌سازی است',
    body: 'سفارش شماره #BZ-1024 با موفقیت ثبت شد و در حال حاضر در مرحله پردازش و بسته‌بندی در انبار مرکزی می‌باشد.',
    createdAt: '2026-07-29T15:00:00Z',
    read: false,
    href: '/account/orders/o-1',
  },
  {
    id: 'n-2',
    kind: 'stock',
    title: 'محصول مورد علاقه شما موجود شد',
    body: `${product(18).title} که به لیست علاقه‌مندی‌های خود اضافه کرده بودید، اکنون در انبار موجود است.`,
    createdAt: '2026-07-29T12:00:00Z',
    read: false,
    href: `/products/${product(18).slug}`,
  },
  {
    id: 'n-3',
    kind: 'offer',
    title: 'کد تخفیف جدید برای شما',
    body: 'به پاس همراهی شما، یک کد تخفیف ۲۰ درصدی برای خریدهای بالای ۵۰۰ هزار تومان به حساب شما منظور شد.',
    createdAt: '2026-07-28T09:30:00Z',
    read: true,
    href: '/account/coupons',
  },
  {
    id: 'n-4',
    kind: 'account',
    title: 'تغییر آدرس با موفقیت انجام شد',
    body: 'آدرس پیش‌فرض شما در حساب کاربری به‌روزرسانی شد. اگر این تغییر را انجام نداده‌اید با پشتیبانی تماس بگیرید.',
    createdAt: '2026-07-26T18:10:00Z',
    read: true,
    href: '/account/addresses',
  },
];

/** Screen 54 — Support tickets. */
export const ticketStatusMeta = {
  open: { label: 'در حال بررسی', tone: 'warning' as const, icon: 'pending' },
  answered: { label: 'پاسخ داده شد', tone: 'mint' as const, icon: 'done_all' },
  closed: { label: 'بسته شده', tone: 'neutral' as const, icon: 'lock' },
};

export const mockTickets: SupportTicket[] = [
  {
    id: 't-1',
    subject: 'پیگیری سفارش شماره BZ-1024',
    status: 'open',
    lastMessage:
      'سلام، سفارش من که دو روز پیش ثبت شده هنوز در وضعیت پردازش است. می‌خواستم بدانم چه زمانی ارسال می‌شود؟',
    lastMessageFromSupport: false,
    updatedAt: '2026-07-29T15:00:00Z',
  },
  {
    id: 't-2',
    subject: 'سوال درباره ابعاد گلدان سرامیکی مینیمال',
    status: 'answered',
    lastMessage: 'با سلام، ابعاد این گلدان در قسمت توضیحات اضافه شد. ارتفاع آن ۲۵ سانتی‌متر می‌باشد.',
    lastMessageFromSupport: true,
    updatedAt: '2026-07-28T11:20:00Z',
  },
  {
    id: 't-3',
    subject: 'تغییر آدرس ارسال',
    status: 'closed',
    lastMessage: 'آدرس شما با موفقیت در سیستم تغییر یافت و بسته به آدرس جدید ارسال خواهد شد.',
    lastMessageFromSupport: true,
    updatedAt: '2026-07-04T10:00:00Z',
  },
];

/** Screen 55 — Reviews. */
export const mockReviews: MyReview[] = [
  {
    id: 'rv-1',
    productSlug: product(8).slug,
    productTitle: product(8).title,
    productImage: product(8).image,
    rating: 4,
    body: 'کیفیت کاغذ بسیار عالی است و طراحی مینیمال آن به من کمک می‌کند تا روی برنامه‌هایم تمرکز کنم. فقط کاش صفحات بیشتری داشت.',
    status: 'published',
    createdAt: '2026-07-04T00:00:00Z',
  },
  {
    id: 'rv-2',
    productSlug: product(11).slug,
    productTitle: product(11).title,
    productImage: product(11).image,
    rating: 5,
    body: 'طراحی فوق‌العاده زیبا و حس خوبی موقع در دست گرفتن می‌دهد. کاملاً ارزش خرید دارد.',
    status: 'published',
    createdAt: '2026-06-15T00:00:00Z',
  },
  {
    id: 'rv-3',
    productSlug: product(0).slug,
    productTitle: product(0).title,
    productImage: product(0).image,
    rating: 5,
    body: 'رنگ‌دانه‌ها بسیار غنی هستند و روی کاغذ آبرنگ عالی می‌نشینند.',
    status: 'pending',
    createdAt: '2026-07-27T00:00:00Z',
  },
];

export const mockAwaitingReviews: AwaitingReview[] = [
  {
    orderId: mockOrderDetails[1]!.id,
    productSlug: product(25).slug,
    productTitle: product(25).title,
    productImage: product(25).image,
    deliveredAt: '2026-07-10T00:00:00Z',
  },
];

/** Screen 58 — Wallet. */
export const mockWalletTransactions: WalletTransaction[] = [
  {
    id: 'w-1',
    title: 'بازگشت وجه سفارش #BZ-1024',
    amount: 120_000,
    createdAt: '2026-07-25T14:30:00Z',
    status: 'success',
    icon: 'arrow_downward',
  },
  {
    id: 'w-2',
    title: 'استفاده در خرید',
    amount: -45_000,
    createdAt: '2026-07-21T09:15:00Z',
    status: 'success',
    icon: 'shopping_bag',
  },
  {
    id: 'w-3',
    title: 'استفاده در خرید',
    amount: -200_000,
    createdAt: '2026-07-14T18:45:00Z',
    status: 'success',
    icon: 'shopping_bag',
  },
  {
    id: 'w-4',
    title: 'افزایش اعتبار از درگاه بانکی',
    amount: 500_000,
    createdAt: '2026-07-02T11:05:00Z',
    status: 'success',
    icon: 'add',
  },
];

/** Screen 59 — Coupons. */
export const mockCoupons: Coupon[] = [
  {
    id: 'c-1',
    code: 'WELCOME',
    title: 'تخفیف اولین خرید',
    condition: 'برای خریدهای بالای ۱ میلیون تومان',
    percent: 10,
    expiresAt: '2026-10-21T00:00:00Z',
    used: false,
  },
  {
    id: 'c-2',
    code: 'HBD-1405',
    title: 'هدیه تولد بوژان',
    condition: 'مخصوص دسته‌بندی هدیه و سبک زندگی',
    amount: 150_000,
    expiresAt: '2026-09-05T00:00:00Z',
    used: false,
  },
  {
    id: 'c-3',
    code: 'SPRING20',
    title: 'جشنواره بهاره',
    condition: 'برای خریدهای بالای ۵۰۰ هزار تومان',
    percent: 20,
    expiresAt: '2026-05-20T00:00:00Z',
    used: true,
  },
];

/** Screen 57 — Recently viewed. */
export const mockRecentlyViewed = [product(8), product(0), product(11), product(25), product(13), product(2)];

/** Screen 60 — Comparison, seeded with two similar notebooks. */
export const mockCompareSlugs = [product(26).slug, product(29).slug];
