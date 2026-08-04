import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, formatPrice, toPersianDigits } from '@bojan/ui';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { SelectionStep } from '@/components/checkout/SelectionStep';
import { getAddresses } from '@/lib/api/account';
import { getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'انتخاب روش ارسال',
  robots: { index: false },
};

/** Screen 73 — Choose a shipping method. */
export default async function CheckoutShippingPage() {
  const [addresses, shippingMethods] = await Promise.all([getAddresses(), getShippingMethods()]);
  const address = addresses.find((item) => item.isDefault) ?? addresses[0];

  return (
    <CheckoutShell
      step="shipping"
      title="انتخاب روش ارسال"
      showSummary
      shippingMethods={shippingMethods}
      nextHref={routes.checkoutDeliveryTime}
      nextLabel="ادامه به زمان تحویل"
      backHref={routes.checkoutAddress}
    >
      {address && (
        <Card className="flex flex-wrap items-start justify-between gap-md p-lg">
          <div className="flex min-w-0 flex-col gap-xs">
            <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
              <Icon name="location_on" size={20} />
              آدرس تحویل
            </h2>
            <p className="text-body-md leading-relaxed text-on-surface-variant">
              {address.province}، {address.city}، {address.line}
            </p>
            <p className="tabular text-caption text-outline">
              گیرنده: {address.recipient} | {toPersianDigits(address.phone)}
            </p>
          </div>

          <Link
            href={routes.checkoutAddress}
            className="shrink-0 text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        </Card>
      )}

      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-card-title text-primary">روش ارسال</h2>
        <SelectionStep
          name="shipping"
          field="shippingMethodId"
          options={shippingMethods.map((method) => ({
            id: method.id,
            title: method.title,
            ...(method.note ? { description: method.note } : null),
            icon: method.icon,
            meta: formatPrice(method.price),
          }))}
        />
      </section>
    </CheckoutShell>
  );
}
