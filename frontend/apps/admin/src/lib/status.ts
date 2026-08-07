import type { BadgeTone } from '@bojan/ui';
import type { AdminOrderStatus } from './types';
import type { AdminReturnStatus } from './api/types';

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

/**
 * Return lifecycle labels — screen 96's queue and its detail.
 *
 * The order mirrors `ReturnStatus` on the backend, which only ever moves
 * forward: submitted → reviewing → approved → received → refunded, with
 * rejected as the way out at any point before the money moves.
 */
export const returnStatusMeta: Record<AdminReturnStatus, StatusMeta> = {
  submitted: { label: 'ثبت‌شده', tone: 'warning', icon: 'assignment_return' },
  reviewing: { label: 'در حال بررسی', tone: 'warning', icon: 'search' },
  approved: { label: 'تأیید شده', tone: 'teal', icon: 'task_alt' },
  received: { label: 'کالا دریافت شد', tone: 'teal', icon: 'inventory_2' },
  refunded: { label: 'بازپرداخت شد', tone: 'mint', icon: 'payments' },
  rejected: { label: 'رد شده', tone: 'error', icon: 'cancel' },
};

/**
 * Where a request may go next, and nowhere else.
 *
 * The backend refuses a backward move, so offering one would be a button that
 * only ever produces an error. Empty for the two closed states — a refunded or
 * rejected request is finished.
 */
export const returnNextStatuses: Record<AdminReturnStatus, AdminReturnStatus[]> = {
  submitted: ['reviewing', 'rejected'],
  reviewing: ['approved', 'rejected'],
  approved: ['received', 'rejected'],
  received: ['refunded', 'rejected'],
  refunded: [],
  rejected: [],
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
