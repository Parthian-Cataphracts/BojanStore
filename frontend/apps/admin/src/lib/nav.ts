import type { AdminRole } from '@/lib/auth/session';

export interface AdminNavItem {
  label: string;
  icon: string;
  href: string;
  /**
   * Who the screen behind this entry admits, where it admits fewer than
   * everyone. Mirrors the `requireRole` call at the top of that page — the page
   * is still the thing that enforces it; this only lets the nav say so.
   *
   * Omitted means every role, which is most of the panel.
   */
  roles?: readonly AdminRole[];
}

export interface AdminNavGroup {
  title?: string;
  items: AdminNavItem[];
}

const OWNER: readonly AdminRole[] = ['owner'];
/** Everyone whose job touches an order — the money side of the panel. */
const ORDERS: readonly AdminRole[] = ['owner', 'sales', 'support'];

/**
 * Sidebar structure from screen 92 (Admin dashboard), grouped to match the
 * ordering of admin screens 91–160.
 */
export const adminNav: AdminNavGroup[] = [
  {
    items: [{ label: 'داشبورد', icon: 'dashboard', href: '/' }],
  },
  {
    title: 'فروش',
    items: [
      { label: 'سفارش‌ها', icon: 'shopping_cart', href: '/orders' },
      // Under the same section permission as orders — an invoice is a view of
      // an order, not a thing an operator can be trusted with separately.
      { label: 'فاکتورها', icon: 'receipt_long', href: '/invoices' },
      // Beside orders, under the same section permission: a return is the money
      // half of an order, and an operator who may see one may see the other.
      { label: 'مرجوعی‌ها', icon: 'assignment_return', href: '/returns', roles: ORDERS },
      // Every account the shop has, shoppers and operators together — the two
      // were separate screens and «who has an account here» had two answers.
      { label: 'کاربران', icon: 'group', href: '/customers', roles: ORDERS },
      { label: 'درخواست‌های سازمانی', icon: 'business_center', href: '/business-requests' },
      // Owner-only, and the screen enforces that itself. Listed for everyone
      // because a nav that hides what it will not let you open is harder to
      // reason about than one that tells you why — `roles` is how it says why.
      { label: 'تأیید شارژ کیف پول', icon: 'account_balance_wallet', href: '/wallet/topups', roles: OWNER },
    ],
  },
  {
    title: 'کاتالوگ',
    items: [
      { label: 'محصولات', icon: 'inventory_2', href: '/products' },
      { label: 'دسته‌بندی‌ها', icon: 'category', href: '/categories' },
      { label: 'برندها', icon: 'branding_watermark', href: '/brands' },
      { label: 'کالکشن‌ها', icon: 'collections_bookmark', href: '/collections' },
      { label: 'موجودی و انبار', icon: 'warehouse', href: '/inventory' },
      { label: 'هشدار کمبود', icon: 'production_quantity_limits', href: '/inventory/low-stock' },
    ],
  },
  {
    title: 'بازاریابی و محتوا',
    items: [
      { label: 'محتوا', icon: 'article', href: '/content' },
      // The screen that edits what the magazine serves. It existed and was
      // linked from nowhere, so the only articles an operator could reach were
      // the ones in the other table — the ones the site never shows.
      { label: 'مقالات مجله', icon: 'menu_book', href: '/content/articles' },
      { label: 'کمپین‌ها', icon: 'campaign', href: '/campaigns' },
      { label: 'کدهای تخفیف', icon: 'sell', href: '/coupons' },
      { label: 'ارسال اعلان', icon: 'send', href: '/campaigns/notifications' },
      { label: 'پشتیبانی', icon: 'support_agent', href: '/support' },
      { label: 'گفتگوی آنلاین', icon: 'chat', href: '/support/live-chat' },
      { label: 'صندوق پستی', icon: 'mail', href: '/support/mailbox' },
      { label: 'پاسخ‌های آماده', icon: 'quickreply', href: '/support/replies' },
    ],
  },
  {
    title: 'گزارش‌ها',
    items: [
      { label: 'گزارش فروش', icon: 'monitoring', href: '/reports/sales' },
      { label: 'گزارش محصولات', icon: 'bar_chart', href: '/reports/products' },
      { label: 'گزارش مالی', icon: 'account_balance', href: '/reports/finance' },
      { label: 'خروجی گرفتن', icon: 'download', href: '/reports/export' },
    ],
  },
  {
    title: 'تنظیمات',
    items: [
      { label: 'تنظیمات فروشگاه', icon: 'settings', href: '/settings' },
      { label: 'سفارش و لغو', icon: 'cancel', href: '/settings/orders' },
      /*
        What the shop charges and what it sends with.

        All three screens existed and none of them was reachable from anywhere —
        no sidebar entry, no link from another page — so the only way to open one
        was to know its URL. That mattered more than it sounds: until they are
        filled in, the shop cannot take an online payment, cannot send a sign-in
        code, and charges whatever shipping price the seeder happened to write.
      */
      { label: 'پرداخت و درگاه', icon: 'credit_card', href: '/settings/payment', roles: OWNER },
      { label: 'ارسال و تحویل', icon: 'local_shipping', href: '/settings/shipping', roles: OWNER },
      { label: 'باشگاه مشتریان', icon: 'workspace_premium', href: '/settings/loyalty', roles: OWNER },
      { label: 'پیامک', icon: 'sms', href: '/settings/sms', roles: OWNER },
      { label: 'اعلان مرورگر', icon: 'notifications_active', href: '/settings/push', roles: OWNER },
      /*
        The panel's own operator list — screen 145.

        «کاربران» above lists everybody — shoppers and operators together —
        because that is the question people actually ask. This screen is the
        narrower one: appointing an operator, setting their role, and the two
        credential rescues. The list sends an operator row here to be edited.
      */
      { label: 'کاربران ادمین', icon: 'admin_panel_settings', href: '/settings/users', roles: OWNER },
      { label: 'نقش‌ها و دسترسی‌ها', icon: 'shield_person', href: '/settings/roles', roles: OWNER },
      { label: 'API و وبهوک', icon: 'webhook', href: '/settings/api', roles: OWNER },
      { label: 'پشتیبان‌گیری', icon: 'backup', href: '/settings/backup', roles: OWNER },
      /*
        Both of these existed and neither was reachable.

        «تاریخچه فعالیت» has been built since screen 147 — the entries are
        written from about forty places in the application — and nothing in the
        panel linked to it, so the only way to open it was to know the address.
        That is the same fault the eight settings screens had, and it is the
        reason the whole audit trail read as a feature nobody had written.

        «لاگ سرور» is the other half, and is new: what the application says
        about itself, which until now left the box only through `docker logs`.
      */
      { label: 'تاریخچه فعالیت', icon: 'history', href: '/settings/audit', roles: OWNER },
      { label: 'لاگ سرور', icon: 'description', href: '/settings/logs', roles: OWNER },
      { label: 'وضعیت سرویس‌ها', icon: 'monitor_heart', href: '/settings/system', roles: OWNER },
      { label: 'پروفایل من', icon: 'account_circle', href: '/settings/profile' },
    ],
  },
];
