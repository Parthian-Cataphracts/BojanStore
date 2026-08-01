'use client';

import { OptionGroup, type Option } from './OptionGroup';
import { useCheckout, type CheckoutSelection } from '@/lib/checkout/store';

/**
 * An `OptionGroup` bound to one field of the guided checkout's selection.
 *
 * The step screens are server components, so they cannot hand `OptionGroup` an
 * `onChange` — which is why every choice used to live and die inside the
 * component. This is the thin client boundary that lets a server-rendered step
 * record what was picked.
 */
export function CheckoutOptionGroup({
  field,
  name,
  options,
  fallback,
  columns = 1,
}: {
  /** Which part of the selection this group sets. */
  field: keyof Pick<CheckoutSelection, 'addressId' | 'shippingMethodId' | 'paymentMethodId'>;
  name: string;
  options: Option[];
  /** Chosen when nothing has been picked yet — the default address, say. */
  fallback?: string;
  columns?: 1 | 2 | 3;
}) {
  const { selection, hydrated, select } = useCheckout();

  const current = selection[field] ?? fallback ?? options[0]?.id ?? '';

  // Rendering before hydration would show the first option selected and then
  // jump once storage is read. Keying on the resolved value remounts the group
  // exactly once, when the real answer arrives.
  return (
    <OptionGroup
      key={hydrated ? `ready:${current}` : 'pending'}
      name={name}
      options={options}
      defaultValue={current}
      onChange={(id) => select({ [field]: id })}
      columns={columns}
    />
  );
}
