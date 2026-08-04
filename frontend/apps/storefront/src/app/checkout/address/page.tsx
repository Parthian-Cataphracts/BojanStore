import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { SelectionStep } from '@/components/checkout/SelectionStep';
import { getAddresses } from '@/lib/api/account';
import { getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'انتخاب آدرس تحویل',
  robots: { index: false },
};

/** Screen 71 — Choose delivery address. */
export default async function CheckoutAddressPage() {
  const [addresses, shippingMethods] = await Promise.all([getAddresses(), getShippingMethods()]);
  const preferred = addresses.find((address) => address.isDefault)?.id ?? addresses[0]?.id;

  return (
    <CheckoutShell
      step="shipping"
      title="آدرس تحویل"
      description="آدرسی را که سفارش باید به آن ارسال شود انتخاب کنید."
      showSummary
      shippingMethods={shippingMethods}
      nextHref={routes.checkoutShipping}
      nextLabel="ادامه به روش ارسال"
      backHref={routes.cart}
    >
      <SelectionStep
        name="address"
        field="addressId"
        {...(preferred ? { fallback: preferred } : null)}
        options={addresses.map((address) => ({
          id: address.id,
          title: address.title,
          description: `${address.province}، ${address.city}، ${address.line}`,
          icon: 'location_on',
          detail: (
            <span className="tabular text-caption text-outline">
              گیرنده: {address.recipient} — {toPersianDigits(address.phone)}
            </span>
          ),
        }))}
      />

      <Link
        href={routes.checkoutNewAddress}
        className={buttonClasses({
          variant: 'outline',
          className: 'gap-sm self-start px-lg',
        })}
      >
        <Icon name="add" size={20} />
        افزودن آدرس
      </Link>
    </CheckoutShell>
  );
}
