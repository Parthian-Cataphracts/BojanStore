import type { AdminRole } from '@/lib/auth/session';

export interface AdminNavItem {
  label: string;
  icon: string;
  href: string;
  /**
   * Who the screen behind this entry admits, where it admits fewer than
   * everyone. Mirrors the `requireRole` call at the top of that page — or, for
   * the settings screens that have none, the owner-only policy the API applies
   * to their resource. The page or the API is still the thing that enforces it;
   * this only lets the nav say so.
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
 * The panel's navigation tree, and the only copy of it: the desktop sidebar,
 * the icon rail and the mobile drawer all render this same array.
 *
 * Grouped by the job being done rather than by the order the screens were
 * drawn in. Two things moved when the groups were redrawn, both because the
 * old shape asked an operator to look in a place the work is not:
 *
 * - Support had been living inside «بازاریابی و محتوا», so the four screens
 *   somebody answering tickets uses all day sat under a heading about
 *   campaigns. It is its own group now.
 * - «کاربران» and «درخواست‌های سازمانی» were under «فروش», which is where an
 *   order lives, not where a person does. They are under «مشتریان» with the
 *   grouping screen that had never been linked from anywhere.
 *
 * Settings is three groups rather than one sixteen-item list — what the shop
 * charges and sends, what the panel itself runs on, and the operator's own
 * account are three different errands, and only the first is one a shop
 * manager runs regularly.
 *
 * Six screens are reachable here that were reachable from nowhere before:
 * four of the report screens, the customer grouping screen and the tax screen.
 * Every one of them was built, routed and orphaned; none of them is new.
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
      // Owner-only, and the screen enforces that itself. Listed for everyone
      // because a nav that hides what it will not let you open is harder to
      // reason about than one that tells you why — `roles` is how it says why.
      {
        label: 'تأیید شارژ کیف پول',
        icon: 'account_balance_wallet',
        href: '/wallet/topups',
        roles: OWNER,
      },
    ],
  },
  {
    title: 'مشتریان',
    items: [
      // Every account the shop has, shoppers and operators together — the two
      // were separate screens and «who has an account here» had two answers.
      { label: 'کاربران', icon: 'group', href: '/customers', roles: ORDERS },
      /*
        The rules that sort those accounts into عادی / وفادار / سازمانی, and who
        currently matches each. Same guard as the list it summarises, and until
        now no screen in the panel linked to it.
      */
      {
        label: 'گروه‌بندی مشتریان',
        icon: 'manage_accounts',
        href: '/customers/groups',
        roles: ORDERS,
      },
      { label: 'درخواست‌های سازمانی', icon: 'business_center', href: '/business-requests' },
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
    ],
  },
  {
    title: 'پشتیبانی',
    items: [
      { label: 'تیکت‌ها', icon: 'support_agent', href: '/support' },
      { label: 'گفتگوی آنلاین', icon: 'chat', href: '/support/live-chat' },
      { label: 'صندوق پستی', icon: 'mail', href: '/support/mailbox' },
      { label: 'پاسخ‌های آماده', icon: 'quickreply', href: '/support/replies' },
    ],
  },
  {
    /*
      Four of these eight were built and linked from nowhere — the sidebar
      carried فروش، محصولات، مالی و خروجی and stopped there, so سفارش‌ها،
      موجودی، مشتریان و کمپین‌ها each had a screen, an endpoint behind it and no
      way in. They are the same four the export screen already offers as
      formats, which is how the gap stayed invisible: the data was reachable,
      the page reading it was not.
    */
    title: 'گزارش‌ها',
    items: [
      { label: 'گزارش فروش', icon: 'monitoring', href: '/reports/sales' },
      { label: 'گزارش سفارش‌ها', icon: 'list_alt', href: '/reports/orders' },
      { label: 'گزارش محصولات', icon: 'bar_chart', href: '/reports/products' },
      { label: 'گزارش موجودی', icon: 'inventory', href: '/reports/inventory' },
      { label: 'گزارش مشتریان', icon: 'person', href: '/reports/customers' },
      { label: 'گزارش کمپین‌ها', icon: 'stacked_bar_chart', href: '/reports/campaigns' },
      { label: 'گزارش مالی', icon: 'account_balance', href: '/reports/finance' },
      { label: 'خروجی گرفتن', icon: 'download', href: '/reports/export' },
    ],
  },
  {
    /*
      What the shop charges, sends and says.

      All of these but the first were reachable from nowhere at one point, and
      it mattered more than it sounds: until they are filled in, the shop cannot
      take an online payment, cannot send a sign-in code, and charges whatever
      shipping price the seeder happened to write.
    */
    title: 'تنظیمات فروشگاه',
    items: [
      { label: 'اطلاعات فروشگاه', icon: 'settings', href: '/settings' },
      { label: 'سفارش و لغو', icon: 'cancel', href: '/settings/orders' },
      { label: 'پرداخت و درگاه', icon: 'credit_card', href: '/settings/payment', roles: OWNER },
      { label: 'ارسال و تحویل', icon: 'local_shipping', href: '/settings/shipping', roles: OWNER },
      // Recorded rather than applied, as the screen itself says — and, until
      // this entry, openable only by typing its address.
      { label: 'مالیات', icon: 'percent', href: '/settings/invoice', roles: OWNER },
      {
        label: 'باشگاه مشتریان',
        icon: 'workspace_premium',
        href: '/settings/loyalty',
        roles: OWNER,
      },
      { label: 'پیامک', icon: 'sms', href: '/settings/sms', roles: OWNER },
      { label: 'اعلان مرورگر', icon: 'notifications_active', href: '/settings/push', roles: OWNER },
    ],
  },
  {
    /*
      The panel's own machinery. Owner-only throughout, and separated from the
      shop's settings because these are the screens somebody opens when
      something is wrong rather than when a price has changed.
    */
    title: 'سیستم و دسترسی',
    items: [
      /*
        «کاربران» under مشتریان lists everybody — shoppers and operators
        together — because that is the question people actually ask. This screen
        is the narrower one: appointing an operator, setting their role, and the
        two credential rescues. The list sends an operator row here to be edited.
      */
      {
        label: 'کاربران ادمین',
        icon: 'admin_panel_settings',
        href: '/settings/users',
        roles: OWNER,
      },
      { label: 'نقش‌ها و دسترسی‌ها', icon: 'shield_person', href: '/settings/roles', roles: OWNER },
      { label: 'API و وبهوک', icon: 'webhook', href: '/settings/api', roles: OWNER },
      { label: 'پشتیبان‌گیری', icon: 'backup', href: '/settings/backup', roles: OWNER },
      // The audit trail is written from about forty places in the application;
      // «لاگ سرور» is what the application says about itself, which before it
      // had a screen left the box only through `docker logs`.
      { label: 'تاریخچه فعالیت', icon: 'history', href: '/settings/audit', roles: OWNER },
      { label: 'لاگ سرور', icon: 'description', href: '/settings/logs', roles: OWNER },
      { label: 'وضعیت سرویس‌ها', icon: 'monitor_heart', href: '/settings/system', roles: OWNER },
    ],
  },
  {
    // The operator's own account rather than the shop's — every role has these,
    // and they were reachable only by way of the profile screen.
    title: 'حساب کاربری',
    items: [
      { label: 'پروفایل من', icon: 'account_circle', href: '/settings/profile' },
      { label: 'تغییر گذرواژه', icon: 'password', href: '/settings/password' },
      { label: 'ورود دو مرحله‌ای', icon: 'security', href: '/settings/two-factor' },
    ],
  },
];
