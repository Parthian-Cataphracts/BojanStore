'use client';

import Link from 'next/link';
import { Card, Icon, toPersianDigits } from '@bojan/ui';
import type { CheckoutPaymentMethod, CheckoutShippingMethod } from '@/lib/api/cart';
import type { Address } from '@/lib/api/types';
import { useCheckoutSelection } from '@/lib/checkout/store';
import { deliverySlots } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

/**
 * Screen 80 — what is currently chosen, with a way back to each step.
 *
 * Every row was a constant: the default address, the first shipping tier,
 * "اولین بازه ممکن" and "پرداخت اینترنتی". On a screen whose entire purpose is
 * to show what will change, that is the one thing it must not do.
 */
export function EditableSelections({
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

  const unset = 'هنوز انتخاب نشده';

  const sections = [
    {
      icon: 'location_on',
      title: 'آدرس تحویل',
      body: address ? `${address.province}، ${address.city}، ${address.line}` : unset,
      detail: address ? `گیرنده: ${address.recipient} — ${toPersianDigits(address.phone)}` : null,
      href: routes.checkoutAddress,
    },
    {
      icon: 'local_shipping',
      title: 'روش ارسال',
      body: shipping?.title ?? unset,
      detail: shipping?.note ?? null,
      href: routes.checkoutShipping,
    },
    {
      icon: 'schedule',
      title: 'زمان تحویل',
      body: day ? `${day.weekday} ${day.day} ${day.month}` : unset,
      detail: slot ? `${slot.label} (${slot.range})` : null,
      href: routes.checkoutDeliveryTime,
    },
    {
      icon: 'credit_card',
      title: 'روش پرداخت',
      body: payment?.title ?? unset,
      detail: payment?.note ?? null,
      href: routes.checkoutPayment,
    },
    {
      icon: 'local_offer',
      title: 'کد تخفیف',
      body: 'اعمال یا حذف کد تخفیف',
      detail: null,
      href: routes.checkoutCoupon,
    },
  ];

  return (
    <div className="grid gap-md md:grid-cols-2">
      {sections.map((section) => (
        <Card key={section.title} className="flex items-start justify-between gap-md p-lg">
          <div className="flex min-w-0 flex-col gap-xs">
            <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
              <Icon name={section.icon} size={20} />
              {section.title}
            </h2>
            <p className="text-body-md leading-relaxed text-on-surface">
              {hydrated ? section.body : '—'}
            </p>
            {hydrated && section.detail && (
              <p className="tabular text-caption text-on-surface-variant">{section.detail}</p>
            )}
          </div>

          <Link
            href={section.href}
            className="shrink-0 text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            تغییر
          </Link>
        </Card>
      ))}
    </div>
  );
}
