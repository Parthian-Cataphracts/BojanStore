'use client';

import Link from 'next/link';
import { Card, Icon, toPersianDigits } from '@bojan/ui';
import type { Address } from '@/lib/api/types';
import { useCheckoutSelection } from '@/lib/checkout/store';
import { routes } from '@/lib/routes';

/**
 * The address the order will actually ship to — screen 79.
 *
 * The default address is only the fallback. A shopper who picked a different
 * one on screen 71 was still shown their default here, on the screen whose
 * whole job is to let them check it.
 */
export function SelectedAddressCard({ addresses }: { addresses: Address[] }) {
  const { selection } = useCheckoutSelection();

  const address =
    addresses.find((item) => item.id === selection.addressId) ??
    addresses.find((item) => item.isDefault) ??
    addresses[0];

  if (!address) return null;

  return (
    <Card className="flex flex-col gap-sm p-lg">
      <div className="flex items-center justify-between gap-md">
        <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
          <Icon name="location_on" size={20} />
          آدرس تحویل
        </h2>
        <Link
          href={routes.checkoutAddress}
          className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
        >
          ویرایش
        </Link>
      </div>

      <p className="text-body-md text-on-surface">{address.recipient}</p>
      <p className="text-body-md leading-relaxed text-on-surface-variant">
        {address.province}، {address.city}، {address.line}
      </p>
      <p className="tabular text-caption text-outline">
        کد پستی: {toPersianDigits(address.postalCode)} · شماره تماس:{' '}
        {toPersianDigits(address.phone)}
      </p>
    </Card>
  );
}
