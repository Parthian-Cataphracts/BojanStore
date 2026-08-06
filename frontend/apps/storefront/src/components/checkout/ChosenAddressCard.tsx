'use client';

import Link from 'next/link';
import { Card, Icon, toPersianDigits } from '@bojan/ui';
import { findChosenAddress } from '@/lib/checkout/address';
import { useCheckout } from '@/lib/checkout/store';
import type { Address } from '@/lib/api/types';
import { routes } from '@/lib/routes';

/**
 * Screen 79's delivery-address card.
 *
 * A client component for the reason `ChosenShippingCard` beside it is: the
 * choice lives in the checkout selection in `sessionStorage`, which a server
 * component cannot read. This screen reached for the shopper's *default*
 * address instead, so anyone ordering to a second address was shown the wrong
 * one on the screen that exists to confirm where the parcel goes — and the
 * order went to the address they actually picked.
 */
export function ChosenAddressCard({ addresses }: { addresses: Address[] }) {
  const { selection, hydrated } = useCheckout();
  const address = findChosenAddress(addresses, selection.addressId);

  return (
    <Card className="flex flex-col gap-sm p-lg">
      <div className="flex items-center justify-between gap-md">
        <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
          <Icon name="place" size={20} />
          آدرس تحویل
        </h2>
        <Link
          href={routes.checkoutAddress}
          className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
        >
          ویرایش
        </Link>
      </div>

      {hydrated && address ? (
        <>
          <p className="text-body-md text-on-surface">{address.recipient}</p>
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            {address.province}، {address.city}، {address.line}
          </p>
          <p className="tabular text-caption text-outline">
            کد پستی: {toPersianDigits(address.postalCode)} · شماره تماس:{' '}
            {toPersianDigits(address.phone)}
          </p>
        </>
      ) : (
        // Nothing chosen, or the selection is not read yet. Naming a different
        // address here is the defect this component exists to fix, so it says
        // what is missing and leaves the edit link above as the way forward.
        <p className="text-body-md text-on-surface-variant">
          {hydrated ? 'هنوز آدرسی انتخاب نشده است.' : '—'}
        </p>
      )}
    </Card>
  );
}
