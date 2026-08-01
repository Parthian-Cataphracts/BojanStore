import type { BadgeTone } from '@bojan/ui';
import type { AdminOrderStatus } from './types';

export interface StatusMeta {
  label: string;
  tone: BadgeTone;
  icon: string;
}

/** Order lifecycle labels shared by every admin order view. */
export const orderStatusMeta: Record<AdminOrderStatus, StatusMeta> = {
  pending: { label: 'در انتظار پرداخت', tone: 'warning', icon: 'schedule' },
  processing: { label: 'در حال آماده‌سازی', tone: 'teal', icon: 'inventory_2' },
  packed: { label: 'بسته‌بندی شده', tone: 'teal', icon: 'package_2' },
  shipped: { label: 'ارسال شده', tone: 'teal', icon: 'local_shipping' },
  delivered: { label: 'تحویل شده', tone: 'mint', icon: 'check_circle' },
  cancelled: { label: 'لغو شده', tone: 'error', icon: 'cancel' },
  returned: { label: 'مرجوع شده', tone: 'neutral', icon: 'assignment_return' },
};

export const productStatusMeta = {
  published: { label: 'منتشر شده', tone: 'mint' as BadgeTone, icon: 'visibility' },
  draft: { label: 'پیش‌نویس', tone: 'warning' as BadgeTone, icon: 'edit_note' },
  archived: { label: 'بایگانی', tone: 'neutral' as BadgeTone, icon: 'archive' },
};

export const contentStatusMeta = {
  published: { label: 'منتشر شده', tone: 'mint' as BadgeTone, icon: 'public' },
  draft: { label: 'پیش‌نویس', tone: 'warning' as BadgeTone, icon: 'edit_note' },
};

export const campaignStatusMeta = {
  scheduled: { label: 'زمان‌بندی شده', tone: 'warning' as BadgeTone, icon: 'schedule' },
  running: { label: 'در حال اجرا', tone: 'mint' as BadgeTone, icon: 'play_circle' },
  ended: { label: 'پایان یافته', tone: 'neutral' as BadgeTone, icon: 'stop_circle' },
};

export const threadStatusMeta = {
  open: { label: 'باز', tone: 'warning' as BadgeTone, icon: 'pending' },
  answered: { label: 'پاسخ داده شد', tone: 'mint' as BadgeTone, icon: 'done_all' },
  closed: { label: 'بسته شده', tone: 'neutral' as BadgeTone, icon: 'lock' },
};

export const healthMeta = {
  operational: { label: 'سالم', tone: 'mint' as BadgeTone, icon: 'check_circle' },
  degraded: { label: 'کند', tone: 'warning' as BadgeTone, icon: 'warning' },
  down: { label: 'قطع', tone: 'error' as BadgeTone, icon: 'error' },
};

/** Low-stock threshold used by the inventory screens and the dashboard alert. */
export const LOW_STOCK_THRESHOLD = 10;
