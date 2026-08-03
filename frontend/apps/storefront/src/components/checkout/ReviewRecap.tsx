'use client';

import Link from 'next/link';
import { Card, Icon } from '@bojan/ui';
import { useCheckout } from '@/lib/checkout/store';
import type { PaymentMethod, ShippingMethod } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

/**
 * Screen 77's recap of the choices made so far.
 *
 * A client component for the same reason `ConfirmSummary` is: every row here
 * except the address is a decision held in the checkout selection, which lives
 * in `sessionStorage`. As a server component this section could only describe
 * the flow in the abstract, and it did — the shipping row read
 * `shippingMethods[0]`, and the delivery window and payment method were the
 * constants "اولین بازه ممکن" and "پرداخت اینترنتی" regardless of what the
 * shopper had picked on the three screens before it. A final review that shows
 * something other than what is about to be ordered is worse than no review.
 */
export function ReviewRecap({
  address,
  shippingMethods,
  paymentMethods,
}: {
  address: string | null;
  shippingMethods: ShippingMethod[];
  paymentMethods: PaymentMethod[];
}) {
  const { selection, hydrated } = useCheckout();

  // An em dash until the selection is read, and where nothing was chosen. The
  // step is still reachable from the "ویرایش" link beside it, which is the
  // point of showing the row at all rather than hiding it.
  const pick = (value: string | undefined) => (hydrated ? (value ?? '—') : '—');

  const shipping = shippingMethods.find((method) => method.id === selection.shippingMethodId);
  const payment = paymentMethods.find((method) => method.id === selection.paymentMethodId);

  const rows = [
    {
      icon: 'place',
      title: 'آدرس تحویل',
      body: address ?? '—',
      href: routes.checkoutAddress,
    },
    {
      icon: 'local_shipping',
      title: 'روش ارسال',
      body: pick(
        shipping && (shipping.note ? `${shipping.label} — ${shipping.note}` : shipping.label),
      ),
      href: routes.checkoutShipping,
    },
    {
      icon: 'schedule',
      title: 'زمان تحویل',
      body: pick(selection.deliveryWindow),
      href: routes.checkoutDeliveryTime,
    },
    {
      icon: 'credit_card',
      title: 'روش پرداخت',
      body: pick(payment?.label),
      href: routes.checkoutPayment,
    },
  ];

  return (
    <section className="grid gap-md md:grid-cols-2">
      {rows.map((row) => (
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
  );
}
