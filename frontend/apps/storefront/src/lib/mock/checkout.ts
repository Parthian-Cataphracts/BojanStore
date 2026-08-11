/**
 * Shipping methods, delivery slots and payment methods.
 * Transcribed from design screens 73, 74 and 75; shared by the single-page
 * checkout (screen 08) and the guided flow (screens 71-80).
 */

import { toPersianDigits } from '@bojan/ui';

export interface ShippingMethod {
  id: string;
  label: string;
  note: string;
  price: number;
  icon: string;
  /**
   * What the goods have to come to for this method to cost nothing.
   *
   * Null or absent means it never does. Set per method by the owner, because a
   * courier that is never free and a post tier that is free over a million are
   * both ordinary and one shop wants both at once.
   */
  freeAboveAmount?: number | null;
}

export const shippingMethods: ShippingMethod[] = [
  {
    id: 'standard',
    label: 'ارسال استاندارد',
    note: 'تحویل ۳ تا ۵ روز کاری در سراسر کشور',
    price: 45_000,
    icon: 'local_shipping',
  },
  {
    id: 'express',
    label: 'ارسال سریع',
    note: 'تهران کمتر از ۲۴ ساعت، مراکز استان ۱ تا ۲ روز کاری',
    price: 85_000,
    icon: 'rocket_launch',
  },
  {
    id: 'courier',
    label: 'پیک ویژه بوژان',
    note: 'تحویل همان روز، فقط داخل تهران',
    price: 120_000,
    icon: 'two_wheeler',
  },
];

export interface PaymentMethod {
  id: string;
  label: string;
  note: string;
  icon: string;
  /** True when the method draws on the wallet balance. */
  usesWallet?: boolean;
  /** True when a shortfall can be collected through a payment gateway. */
  requiresGateway?: boolean;
}

export const paymentMethods: PaymentMethod[] = [
  {
    id: 'gateway',
    label: 'پرداخت اینترنتی',
    note: 'درگاه بانکی امن',
    icon: 'credit_card',
    requiresGateway: true,
  },
  {
    id: 'wallet',
    label: 'کیف پول بوژان',
    // Left to the screen, which reads the real balance — a figure hard-coded
    // here would be wrong for everyone.
    note: '',
    icon: 'account_balance_wallet',
    usesWallet: true,
    requiresGateway: true,
  },
  {
    id: 'cod',
    label: 'پرداخت در محل',
    note: 'فقط برای سفارش‌های داخل تهران',
    icon: 'payments',
  },
];

/** Time windows offered on screen 74. */
export const deliverySlots = [
  { id: 'morning', label: 'صبح', range: '۹ تا ۱۳' },
  { id: 'afternoon', label: 'بعدازظهر', range: '۱۴ تا ۱۸' },
  { id: 'evening', label: 'عصر', range: '۱۸ تا ۲۱' },
];

/** The next five delivery days, labelled the way screen 74 draws them. */
export function upcomingDeliveryDays(count = 5) {
  const weekdays = ['یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه', 'شنبه'];
  const today = new Date();

  return Array.from({ length: count }, (_, index) => {
    const date = new Date(today);
    date.setDate(today.getDate() + index);

    const parts = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
      day: 'numeric',
      month: 'long',
    }).formatToParts(date);

    return {
      id: `day-${index}`,
      // "امروز" and "فردا" read better than a weekday name for the first two.
      weekday: index === 0 ? 'امروز' : index === 1 ? 'فردا' : (weekdays[date.getDay()] ?? ''),
      day: toPersianDigits(parts.find((part) => part.type === 'day')?.value ?? ''),
      month: parts.find((part) => part.type === 'month')?.value ?? '',
    };
  });
}
