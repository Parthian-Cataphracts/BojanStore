'use client';

import Link from 'next/link';
import { Card, Icon } from '@bojan/ui';
import type { CheckoutPaymentMethod, CheckoutShippingMethod } from '@/lib/api/cart';
import type { Address } from '@/lib/api/types';
import { useCheckoutSelection } from '@/lib/checkout/store';
import { deliverySlots } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

/**
 * What the shopper actually chose, on the review and confirm screens.
 *
 * Every row here used to be a constant: the default address, the first
 * shipping tier, "اولین بازه ممکن", and "پرداخت اینترنتی" — regardless of what
 * had been picked. A review screen that shows something other than the order
 * about to be placed is worse than no review screen, because it is the one
 * place the shopper is asked to check.
 */
export function SelectionRecap({
  addresses,
  shippingMethods,
  paymentMethods,
  days,
}: {
  addresses: Address[];
  shippingMethods: CheckoutShippingMethod[];
  paymentMethods: CheckoutPaymentMethod[];
  days: Array<{ id: string; weekday: string; day: string; month: string }>;
}) {
  const { selection, hydrated } = useCheckoutSelection();

  const address =
    addresses.find((item) => item.id === selection.addressId) ??
    addresses.find((item) => item.isDefault) ??
    addresses[0];

  const shipping =
    shippingMethods.find((method) => method.id === selection.shippingMethodId) ??
    shippingMethods[0];

  const payment = paymentMethods.find((method) => method.id === selection.paymentMethodId);

  const day = days.find((option) => option.id === selection.deliveryDayId) ?? days[0];
  const slot =
    deliverySlots.find((option) => option.id === selection.deliverySlotId) ?? deliverySlots[0];

  // Until the stored selection is read, saying anything definite would be
  // saying the wrong thing.
  const pending = '—';

  const rows = [
    {
      icon: 'location_on',
      title: 'آدرس تحویل',
      body: address ? `${address.province}، ${address.city}، ${address.line}` : 'انتخاب نشده',
      href: routes.checkoutAddress,
    },
    {
      icon: 'local_shipping',
      title: 'روش ارسال',
      body: shipping
        ? [shipping.title, shipping.note].filter(Boolean).join(' — ')
        : 'انتخاب نشده',
      href: routes.checkoutShipping,
    },
    {
      icon: 'schedule',
      title: 'زمان تحویل',
      body:
        day && slot
          ? `${day.weekday} ${day.day} ${day.month} — ${slot.label} (${slot.range})`
          : 'انتخاب نشده',
      href: routes.checkoutDeliveryTime,
    },
    {
      icon: 'credit_card',
      title: 'روش پرداخت',
      body: payment
        ? [payment.title, payment.note].filter(Boolean).join(' — ')
        : 'انتخاب نشده',
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
            <p className="text-body-md leading-relaxed text-on-surface-variant">
              {hydrated ? row.body : pending}
            </p>
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
