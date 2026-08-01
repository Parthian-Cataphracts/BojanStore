import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Card, Icon, formatPrice, toPersianDigits } from '@bojan/ui';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { getAddresses } from '@/lib/api/account';
import { mockCart } from '@/lib/mock/catalog';
import { shippingMethods } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'بررسی نهایی سفارش',
  robots: { index: false },
};

/** Screen 77 — Final review before payment. */
export default async function CheckoutReviewPage() {
  const addresses = await getAddresses();
  const address = addresses.find((item) => item.isDefault) ?? addresses[0];
  const shipping = shippingMethods[0]!;

  return (
    <CheckoutShell
      step="payment"
      title="بررسی نهایی سفارش"
      cart={mockCart}
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

        <Card className="divide-y divide-paper-border">
          {mockCart.lines.map((line) => (
            <div key={line.id} className="flex items-center gap-md p-md">
              <span className="relative h-16 w-16 shrink-0 overflow-hidden rounded border border-outline-variant">
                <Image src={line.image} alt={line.title} fill sizes="64px" className="object-cover" />
              </span>
              <span className="flex min-w-0 flex-1 flex-col gap-xs">
                <span className="line-clamp-2 text-body-md text-on-surface">{line.title}</span>
                <span className="tabular text-caption text-on-surface-variant">
                  تعداد: {toPersianDigits(line.quantity)}
                </span>
              </span>
              <span className="tabular shrink-0 text-label-md font-semibold text-primary">
                {formatPrice(line.unitPrice * line.quantity)}
              </span>
            </div>
          ))}
        </Card>
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
