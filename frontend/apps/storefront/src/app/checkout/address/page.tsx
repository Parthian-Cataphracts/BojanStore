import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { OptionGroup } from '@/components/checkout/OptionGroup';
import { getAddresses } from '@/lib/api/account';
import { mockCart } from '@/lib/mock/catalog';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'انتخاب آدرس تحویل',
  robots: { index: false },
};

/** Screen 71 — Choose delivery address. */
export default async function CheckoutAddressPage() {
  const addresses = await getAddresses();

  return (
    <CheckoutShell
      step="shipping"
      title="آدرس تحویل"
      description="آدرسی را که سفارش باید به آن ارسال شود انتخاب کنید."
      cart={mockCart}
      nextHref={routes.checkoutShipping}
      nextLabel="ادامه به روش ارسال"
      backHref={routes.cart}
    >
      <OptionGroup
        name="address"
        defaultValue={addresses.find((address) => address.isDefault)?.id}
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
