import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, formatPrice } from '@bojan/ui';
import { CartLineList } from '@/components/checkout/CartLineList';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { getAddresses } from '@/lib/api/account';
import { getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'بررسی نهایی سفارش',
  robots: { index: false },
};

/** Screen 77 — Final review before payment. */
export default async function CheckoutReviewPage() {
  const shippingMethods = await getShippingMethods();
  const addresses = await getAddresses();
  const address = addresses.find((item) => item.isDefault) ?? addresses[0];
  const shipping = shippingMethods[0]!;

  return (
    <CheckoutShell
      step="payment"
      title="بررسی نهایی سفارش"
      showSummary
      extraRows={[{ label: 'هزینه ارسال', value: formatPrice(shipping.price) }]}
      nextHref={routes.checkoutConfirm}
      nextLabel="تایید و ادامه"
      backHref={routes.checkoutPayment}
    >
      {/* Items */}
      <section className="flex flex-col gap-md">
        <div className="flex items-center justify-between gap-md">
          <h2 className="font-headline text-card-title text-primary">محصولات</h2>
          <Link
            href={routes.cart}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        </div>

        <CartLineList />
      </section>

      {/* Delivery + payment recap */}
      <section className="grid gap-md md:grid-cols-2">
        {[
          {
            icon: 'location_on',
            title: 'آدرس تحویل',
            body: address ? `${address.province}، ${address.city}، ${address.line}` : '—',
            href: routes.checkoutAddress,
          },
          {
            icon: 'local_shipping',
            title: 'روش ارسال',
            body: `${shipping.label} — ${shipping.note}`,
            href: routes.checkoutShipping,
          },
          {
            icon: 'schedule',
            title: 'زمان تحویل',
            body: 'اولین بازه ممکن',
            href: routes.checkoutDeliveryTime,
          },
          {
            icon: 'credit_card',
            title: 'روش پرداخت',
            body: 'پرداخت اینترنتی (درگاه بانکی امن)',
            href: routes.checkoutPayment,
          },
        ].map((row) => (
          <Card key={row.title} className="flex items-start justify-between gap-md p-lg">
            <div className="flex min-w-0 flex-col gap-xs">
              <span className="flex items-center gap-xs text-label-md font-semibold text-primary">
                <Icon name={row.icon} size={20} />
                {row.title}
              </span>
              <p className="text-body-md leading-relaxed text-on-surface-variant">{row.body}</p>
            </div>
            <Link
              href={row.href}
              className="shrink-0 text-label-md font-semibold text-secondary transition-colors hover:text-primary"
            >
              ویرایش
            </Link>
          </Card>
        ))}
      </section>
    </CheckoutShell>
  );
}
