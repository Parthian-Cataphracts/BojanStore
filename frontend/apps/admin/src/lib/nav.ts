export interface AdminNavItem {
  label: string;
  icon: string;
  href: string;
}

export interface AdminNavGroup {
  title?: string;
  items: AdminNavItem[];
}

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
      { label: 'مشتریان', icon: 'group', href: '/customers' },
      { label: 'درخواست‌های سازمانی', icon: 'business_center', href: '/business-requests' },
      // Owner-only, and the screen enforces that itself. Listed for everyone
      // because a nav that hides what it will not let you open is harder to
      // reason about than one that tells you why.
      { label: 'تأیید شارژ کیف پول', icon: 'account_balance_wallet', href: '/wallet/topups' },
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
      { label: 'کمپین‌ها', icon: 'campaign', href: '/campaigns' },
      { label: 'کدهای تخفیف', icon: 'sell', href: '/coupons' },
      { label: 'ارسال اعلان', icon: 'send', href: '/campaigns/notifications' },
      { label: 'پشتیبانی', icon: 'support_agent', href: '/support' },
      { label: 'گفتگوی آنلاین', icon: 'chat', href: '/support/live-chat' },
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
      { label: 'کاربران ادمین', icon: 'admin_panel_settings', href: '/settings/users' },
      { label: 'نقش‌ها و دسترسی‌ها', icon: 'shield_person', href: '/settings/roles' },
      { label: 'API و وبهوک', icon: 'webhook', href: '/settings/api' },
      { label: 'پشتیبان‌گیری', icon: 'backup', href: '/settings/backup' },
      { label: 'پروفایل من', icon: 'account_circle', href: '/settings/profile' },
    ],
  },
];
