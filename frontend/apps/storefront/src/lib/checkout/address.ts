import type { Address } from '@/lib/api/types';

/**
 * The address the shopper picked on screen 71, resolved against their own list.
 *
 * Screens 77 and 79 used to reach for `addresses.find(isDefault) ?? addresses[0]`
 * instead. The selection lives in `sessionStorage`, so a server component
 * genuinely cannot see it — but the answer was to move the row into a client
 * component, which is what every other row on those screens already does, not
 * to substitute a different address. A shopper who ordered to their office was
 * shown their home address on both screens that exist to confirm the
 * destination, and the parcel went to the office.
 *
 * `undefined` when nothing is chosen yet, or when the stored id no longer
 * matches an address the shopper has — a deleted address must not silently
 * resolve to a different one.
 */
export function findChosenAddress(
  addresses: readonly Address[],
  addressId: string | undefined,
): Address | undefined {
  if (!addressId) return undefined;
  return addresses.find((address) => address.id === addressId);
}

/** The one-line form the checkout screens render. */
export function describeAddress(address: Address | undefined): string | undefined {
  if (!address) return undefined;
  return `${address.province}، ${address.city}، ${address.line}`;
}
