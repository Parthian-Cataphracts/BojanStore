import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';
import { getOrders } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'سفارش ثبت شد',
  robots: { index: false },
};

/** Screen 31 — Order placed. */
export default async function OrderPlacedPage() {
  // The most recent order stands in for the one just created.
  const [order] = await getOrders();

  return (
    <StatusScreen
      icon="check_circle"
      tone="success"
      title="سفارش شما ثبت شد"
      message="سفارش شما با موفقیت در بوژان ثبت شد و مراحل آماده‌سازی آن به‌زودی شروع می‌شود."
      details={
        order
          ? [
              { label: 'شماره سفارش', value: `#${order.number}` },
              { label: 'زمان تقریبی تحویل', value: '۲ تا ۴ روز کاری' },
              { label: 'تعداد کالاها', value: `${toPersianDigits(order.itemCount)} کالا` },
              { label: 'مبلغ پرداخت‌شده', value: formatPrice(order.total) },
              { label: 'روش ارسال', value: 'ارسال استاندارد' },
            ]
          : []
      }
      actions={
        <>
          <Link
            href={order ? `${routes.orders}/${order.id}` : routes.orders}
            className={buttonClasses({ size: 'lg', fullWidth: true })}
          >
            مشاهده جزئیات سفارش
          </Link>
          <Link
            href={routes.home}
            className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
          >
            بازگشت به صفحه اصلی
          </Link>
        </>
      }
      note="برای پیگیری سفارش می‌توانید از بخش سفارش‌های من استفاده کنید."
    >
      <p className="flex items-center gap-xs text-caption text-on-surface-variant">
        <Icon name="local_shipping" size={18} />
        زمان تقریبی تحویل: ۲ تا ۴ روز کاری
      </p>
    </StatusScreen>
  );
}
