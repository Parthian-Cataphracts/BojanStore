'use client';

import { OptionGroup, type Option } from './OptionGroup';
import { useCheckoutSelection, type CheckoutSelection } from '@/lib/checkout/store';

/**
 * One question of the guided checkout, bound to the shared selection.
 *
 * Every step used to render an `OptionGroup` with no `onChange`, so the choice
 * lived and died inside that component: pressing "continue" navigated away and
 * the next screen had no idea what had been picked. Naming the field here is
 * what connects the radio group to the order that eventually gets placed.
 */
export function SelectionStep({
  name,
  field,
  options,
  fallback,
}: {
  name: string;
  field: keyof CheckoutSelection;
  options: Option[];
  /** Used when nothing is stored yet — normally the shopper's default address or the first tier. */
  fallback?: string;
}) {
  const { selection, hydrated, select } = useCheckoutSelection();

  // Rendering before the stored selection is read would mount the group with
  // the fallback checked and then jump; `key` remounts it once, with the right
  // option already selected.
  const current = (selection[field] as string | undefined) ?? fallback ?? options[0]?.id;
  const known = options.some((option) => option.id === current);
  const value = known ? current : (fallback ?? options[0]?.id);

  return (
    <OptionGroup
      key={hydrated ? `hydrated-${value}` : 'pending'}
      name={name}
      options={options}
      {...(value ? { defaultValue: value } : null)}
      onChange={(id) => select({ [field]: id } as CheckoutSelection)}
    />
  );
}
