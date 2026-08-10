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

/**
 * Screen 32 — Payment successful.
 *
 * The order number arrives in the query string, put there by the callback page
 * after the API confirmed the payment with the gateway. It is used to find the
 * order among the customer's own, which is the only reason it can be trusted:
 * a number that is not theirs matches nothing and the page falls back to
 * showing no order rather than someone else's.
 *
 * It used to take the customer's most recent order regardless — which is the
 * right one most of the time and the wrong one exactly when it matters, for a
 * shopper who placed a second order while the first was still unpaid. There was
 * also a hard-coded reference standing in when the gateway sent none, so a
 * payment that returned nothing still showed a plausible-looking tracking
 * number.
 */
export default async function PaymentSuccessPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const orderNumber = first(params.order);
  const reference = first(params.ref);

  const orders = await getOrders();
  const order = orderNumber
    ? orders.find((candidate) => candidate.number === orderNumber)
    : undefined;

  return (
    <StatusScreen
      icon="check_circle"
      tone="success"
      title="پرداخت با موفقیت انجام شد"
      message="پرداخت شما با موفقیت ثبت شد و سفارش برای آماده‌سازی به تیم بوژان ارسال شد."
      details={[
        ...(order
          ? [
              { label: 'شماره سفارش', value: `#${order.number}` },
              { label: 'مبلغ پرداختی', value: formatPrice(order.total) },
            ]
          : orderNumber
            ? [{ label: 'شماره سفارش', value: `#${orderNumber}` }]
            : []),
        // Only when the gateway actually returned one. Nothing invented: a
        // reference the shopper quotes at support has to be a reference support
        // can look up.
        ...(reference ? [{ label: 'کد پیگیری پرداخت', value: reference }] : []),
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
