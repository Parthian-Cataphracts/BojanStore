import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';
import { getOrders } from '@/lib/api/account';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'پرداخت موفق',
  robots: { index: false },
};

/** Screen 32 — Payment successful. */
export default async function PaymentSuccessPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  // The gateway returns its own reference; fall back to a placeholder in dev.
  const reference = first(params.ref) ?? '۷۸۴۵۶۲۱';
  const [order] = await getOrders();

  return (
    <StatusScreen
      icon="check_circle"
      tone="success"
      title="پرداخت با موفقیت انجام شد"
      message="پرداخت شما با موفقیت ثبت شد و سفارش برای آماده‌سازی به تیم بوژان ارسال شد."
      details={[
        { label: 'کد پیگیری پرداخت', value: reference },
        ...(order
          ? [
              { label: 'مبلغ پرداختی', value: formatPrice(order.total) },
              { label: 'شماره سفارش', value: `#${order.number}` },
            ]
          : []),
      ]}
      actions={
        <>
          <Link
            href={order ? `${routes.orders}/${order.id}` : routes.orders}
            className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm' })}
          >
            مشاهده سفارش
            <Icon name="receipt_long" size={20} />
          </Link>
          <Link
            href={routes.products}
            className={buttonClasses({
              variant: 'outline',
              size: 'lg',
              fullWidth: true,
              className: 'gap-sm',
            })}
          >
            ادامه خرید
            <Icon name="arrow_back" size={20} />
          </Link>
        </>
      }
    />
  );
}
