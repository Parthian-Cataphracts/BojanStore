import type { AdminRole } from '@/lib/auth/session';
// Type-only, so the cycle between this file and the one that derives the
// permission catalogue from it is erased before anything runs.
import type { SectionKey } from '@/lib/permissions';

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
  /**
   * The coarse area this screen belongs to, and — with `href` — what makes it
   * grantable on its own.
   *
   * An owner ticks a whole section or the individual screens inside it; both
   * are stored as grants, and `lib/permissions` reads the section as covering
   * everything under it. So this field is not decoration: a screen without one
   * is a screen no operator can be narrowed away from, which is right for the
   * dashboard and for the three screens about the operator's own account, and
   * wrong for everything else. A new screen that forgets it is one every
   * narrowed operator can open.
   *
   * The same routes appear in `PanelScreen.Sections` on the API, which is what
   * turns a grant into an answer about an endpoint. A key here that is missing
   * there cannot be saved — the checkbox will not stick — which is the failure
   * to look for if a new entry refuses to be granted.
   */
  section?: SectionKey;
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
 *
 * It is also the permission catalogue. Every entry carrying a `section` is
 * something an owner can grant on its own, and `lib/permissions` derives the
 * checklist on «کاربران ادمین» from this array rather than from a second list
 * that a new screen would get left out of. So what is written here decides
 * three things at once: where a screen appears, whether it can be granted, and
 * whether a narrowed operator sees it at all.
 */
export const adminNav: AdminNavGroup[] = [
  {
    items: [{ label: 'داشبورد', icon: 'dashboard', href: '/' }],
  },
  {
    title: 'فروش',
    items: [
      { label: 'سفارش‌ها', icon: 'shopping_cart', href: '/orders', section: 'orders' },
      // In the orders section, and grantable on its own: an invoice is a view
      // of an order, so the endpoints behind the two are the same — but an
      // owner who wants somebody printing invoices and not touching the queue
      // can now say so, and the queue will not be in that person's menu.
      { label: 'فاکتورها', icon: 'receipt_long', href: '/invoices', section: 'orders' },
      // The entry that made the finer grain necessary. Returns and orders are
      // one section on the API, so «مرجوعی‌ها but not سفارش‌ها» could not be
      // expressed at all until a screen became grantable by itself.
      {
        label: 'مرجوعی‌ها',
        icon: 'assignment_return',
        href: '/returns',
        section: 'orders',
        roles: ORDERS,
      },
      // Owner-only, and the screen enforces that itself. Still drawn for the
      // other roles, with a lock — `roles` is a job title, and knowing that the
      // wallet queue belongs to the owner is worth more than a menu that
      // silently differs between colleagues. `section` is the opposite case: it
      // is a decision about one person, so what it withholds is left out
      // entirely rather than advertised. See `lib/permissions`.
      {
        label: 'تأیید شارژ کیف پول',
        icon: 'account_balance_wallet',
        href: '/wallet/topups',
        section: 'customers',
        roles: OWNER,
      },
    ],
  },
  {
    title: 'مشتریان',
    items: [
      // Every account the shop has, shoppers and operators together — the two
      // were separate screens and «who has an account here» had two answers.
      { label: 'کاربران', icon: 'group', href: '/customers', section: 'customers', roles: ORDERS },
      /*
        The rules that sort those accounts into عادی / وفادار / سازمانی, and who
        currently matches each. Same guard as the list it summarises, and until
        now no screen in the panel linked to it.
      */
      {
        label: 'گروه‌بندی مشتریان',
        icon: 'manage_accounts',
        href: '/customers/groups',
        section: 'customers',
        roles: ORDERS,
      },
      {
        label: 'درخواست‌های سازمانی',
        icon: 'business_center',
        href: '/business-requests',
        section: 'business',
      },
    ],
  },
  {
    title: 'کاتالوگ',
    items: [
      { label: 'محصولات', icon: 'inventory_2', href: '/products', section: 'products' },
      { label: 'دسته‌بندی‌ها', icon: 'category', href: '/categories', section: 'products' },
      { label: 'برندها', icon: 'branding_watermark', href: '/brands', section: 'products' },
      {
        label: 'کالکشن‌ها',
        icon: 'collections_bookmark',
        href: '/collections',
        section: 'products',
      },
      { label: 'موجودی و انبار', icon: 'warehouse', href: '/inventory', section: 'inventory' },
      {
        label: 'هشدار کمبود',
        icon: 'production_quantity_limits',
        href: '/inventory/low-stock',
        section: 'inventory',
      },
    ],
  },
  {
    title: 'بازاریابی و محتوا',
    items: [
      { label: 'محتوا', icon: 'article', href: '/content', section: 'content' },
      // The screen that edits what the magazine serves. It existed and was
      // linked from nowhere, so the only articles an operator could reach were
      // the ones in the other table — the ones the site never shows.
      { label: 'مقالات مجله', icon: 'menu_book', href: '/content/articles', section: 'content' },
      { label: 'کمپین‌ها', icon: 'campaign', href: '/campaigns', section: 'campaigns' },
      { label: 'کدهای تخفیف', icon: 'sell', href: '/coupons', section: 'campaigns' },
      {
        label: 'ارسال اعلان',
        icon: 'send',
        href: '/campaigns/notifications',
        section: 'campaigns',
      },
    ],
  },
  {
    title: 'پشتیبانی',
    items: [
      { label: 'تیکت‌ها', icon: 'support_agent', href: '/support', section: 'support' },
      { label: 'گفتگوی آنلاین', icon: 'chat', href: '/support/live-chat', section: 'support' },
      { label: 'صندوق پستی', icon: 'mail', href: '/support/mailbox', section: 'support' },
      { label: 'پاسخ‌های آماده', icon: 'quickreply', href: '/support/replies', section: 'support' },
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
      { label: 'گزارش فروش', icon: 'monitoring', href: '/reports/sales', section: 'reports' },
      { label: 'گزارش سفارش‌ها', icon: 'list_alt', href: '/reports/orders', section: 'reports' },
      { label: 'گزارش محصولات', icon: 'bar_chart', href: '/reports/products', section: 'reports' },
      { label: 'گزارش موجودی', icon: 'inventory', href: '/reports/inventory', section: 'reports' },
      { label: 'گزارش مشتریان', icon: 'person', href: '/reports/customers', section: 'reports' },
      {
        label: 'گزارش کمپین‌ها',
        icon: 'stacked_bar_chart',
        href: '/reports/campaigns',
        section: 'reports',
      },
      {
        label: 'گزارش مالی',
        icon: 'account_balance',
        href: '/reports/finance',
        section: 'reports',
      },
      { label: 'خروجی گرفتن', icon: 'download', href: '/reports/export', section: 'reports' },
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
      { label: 'اطلاعات فروشگاه', icon: 'settings', href: '/settings', section: 'settings' },
      { label: 'سفارش و لغو', icon: 'cancel', href: '/settings/orders', section: 'settings' },
      {
        label: 'پرداخت و درگاه',
        icon: 'credit_card',
        href: '/settings/payment',
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'ارسال و تحویل',
        icon: 'local_shipping',
        href: '/settings/shipping',
        section: 'settings',
        roles: OWNER,
      },
      // Recorded rather than applied, as the screen itself says — and, until
      // this entry, openable only by typing its address.
      {
        label: 'مالیات',
        icon: 'percent',
        href: '/settings/invoice',
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'باشگاه مشتریان',
        icon: 'workspace_premium',
        href: '/settings/loyalty',
        section: 'settings',
        roles: OWNER,
      },
      { label: 'پیامک', icon: 'sms', href: '/settings/sms', section: 'settings', roles: OWNER },
      {
        label: 'اعلان مرورگر',
        icon: 'notifications_active',
        href: '/settings/push',
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'تایید ایمیل و شماره',
        icon: 'verified_user',
        href: '/settings/verification',
        section: 'settings',
        roles: OWNER,
      },
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
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'نقش‌ها و دسترسی‌ها',
        icon: 'shield_person',
        href: '/settings/roles',
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'API و وبهوک',
        icon: 'webhook',
        href: '/settings/api',
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'پشتیبان‌گیری',
        icon: 'backup',
        href: '/settings/backup',
        section: 'settings',
        roles: OWNER,
      },
      // The audit trail is written from about forty places in the application;
      // «لاگ سرور» is what the application says about itself, which before it
      // had a screen left the box only through `docker logs`.
      {
        label: 'تاریخچه فعالیت',
        icon: 'history',
        href: '/settings/audit',
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'لاگ سرور',
        icon: 'description',
        href: '/settings/logs',
        section: 'settings',
        roles: OWNER,
      },
      {
        label: 'وضعیت سرویس‌ها',
        icon: 'monitor_heart',
        href: '/settings/system',
        section: 'settings',
        roles: OWNER,
      },
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
