import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';
import { getOrder, getOrders } from '@/lib/api/account';
import { getStoreSettings } from '@/lib/api/store';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'سفارش ثبت شد',
  robots: { index: false },
};

/** Screen 31 — Order placed. */
export default async function OrderPlacedPage() {
  // The most recent order stands in for the one just created.
  const [{ identity, promises }, [summary]] = await Promise.all([getStoreSettings(), getOrders()]);

  // The detail rather than the summary, for one field: the shipping method the
  // shopper actually chose. This row used to read «ارسال استاندارد» whatever
  // they picked, so someone who paid for express delivery was told on the
  // confirmation screen that they had not.
  const order = summary ? await getOrder(summary.id) : null;

  return (
    <StatusScreen
      icon="check_circle"
      tone="success"
      title="سفارش شما ثبت شد"
      message={`سفارش شما با موفقیت در ${identity.name} ثبت شد و مراحل آماده‌سازی آن به‌زودی شروع می‌شود.`}
      details={
        order
          ? [
              { label: 'شماره سفارش', value: `#${order.number}` },
              { label: 'زمان تقریبی تحویل', value: promises.deliveryEstimate },
              { label: 'تعداد کالاها', value: `${toPersianDigits(order.itemCount)} کالا` },
              { label: 'مبلغ پرداخت‌شده', value: formatPrice(order.total) },
              { label: 'روش ارسال', value: order.shippingMethod },
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
        زمان تقریبی تحویل: {promises.deliveryEstimate}
      </p>
    </StatusScreen>
  );
}
