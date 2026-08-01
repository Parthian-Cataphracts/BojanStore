/**
 * Return requests, matching the content on design screens 35 and 36.
 */

import type { ReturnRequest, ReturnStatus, ReturnTimelineStep } from '../api/types';
import { mockOrderDetails } from './orders';

/** Reason options offered by the return form on screen 35. */
export const returnReasons = [
  'نقص فنی یا ظاهری کالا',
  'مغایرت با سفارش ثبت شده',
  'آسیب‌دیدگی در ارسال',
  'سایر دلایل',
] as const;

export const returnStatusMeta: Record<
  ReturnStatus,
  { label: string; tone: 'mint' | 'teal' | 'warning' | 'error' | 'neutral'; icon: string }
> = {
  submitted: { label: 'ثبت شده', tone: 'neutral', icon: 'receipt_long' },
  reviewing: { label: 'در حال بررسی', tone: 'teal', icon: 'sync' },
  approved: { label: 'تأیید شده', tone: 'mint', icon: 'fact_check' },
  received: { label: 'کالا دریافت شد', tone: 'teal', icon: 'inventory_2' },
  refunded: { label: 'وجه بازگشت داده شد', tone: 'mint', icon: 'currency_exchange' },
  rejected: { label: 'رد شده', tone: 'error', icon: 'cancel' },
};

/** The five-stage return journey from screen 36, cut off at `reached`. */
function returnTimeline(reached: number): ReturnTimelineStep[] {
  const stages = [
    { id: 'submitted', label: 'ثبت درخواست', description: 'با موفقیت ثبت شد', icon: 'check' },
    {
      id: 'reviewing',
      label: 'بررسی پشتیبانی',
      description: 'در حال پیگیری توسط کارشناسان',
      icon: 'support_agent',
    },
    { id: 'approved', label: 'تأیید مرجوعی', description: 'در انتظار تأیید', icon: 'fact_check' },
    { id: 'received', label: 'دریافت کالا', description: 'توسط انبار', icon: 'inventory_2' },
    {
      id: 'refunded',
      label: 'بازگشت وجه / تعویض',
      description: 'مرحله نهایی',
      icon: 'currency_exchange',
    },
  ];

  return stages.map((stage, index) => ({
    ...stage,
    state: index < reached ? 'done' : index === reached ? 'current' : 'upcoming',
  }));
}

const sourceOrder = mockOrderDetails[1]!;
const sourceItem = sourceOrder.items[0]!;

export const mockReturns: ReturnRequest[] = [
  {
    id: 'r-1',
    code: 'RT-BZ-204',
    orderId: sourceOrder.id,
    orderNumber: sourceOrder.number,
    productSlug: sourceItem.slug,
    productTitle: sourceItem.title,
    productImage: sourceItem.image,
    quantity: 1,
    reason: 'مغایرت با سفارش ثبت شده',
    status: 'reviewing',
    createdAt: '2026-07-24T09:10:00Z',
    timeline: returnTimeline(1),
  },
];
