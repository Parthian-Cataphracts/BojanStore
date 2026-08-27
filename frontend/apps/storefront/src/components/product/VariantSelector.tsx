'use client';

import { useEffect, useState } from 'react';
import { Icon, cn } from '@bojan/ui';
import type { ProductSku, ProductVariantAxis } from '@/lib/api/types';

/**
 * Screen 86 — colour swatches and size chips.
 * Unavailable options stay visible but disabled, so the shopper can see what
 * exists rather than wondering why a size vanished.
 *
 * What counts as available is read from the combinations themselves, not from
 * the flag on the option. That flag is set by hand on the panel's variant form
 * and nothing keeps it in step with the warehouse: a size whose last one had
 * sold was still offered, and a size an operator had listed but never given a
 * combination looked exactly like one they had. Both are «not for sale», and
 * the only record that knows it is the SKU table.
 */
export function VariantSelector({
  axes,
  skus = [],
  onChange,
}: {
  axes: ProductVariantAxis[];
  /**
   * The product's sellable combinations. Empty is «nothing to go on» rather
   * than «nothing is available» — a product with no combinations at all falls
   * back to the option's own flag, which is all there is to read.
   */
  skus?: ProductSku[];
  /**
   * Fires with the axis-order combination key (`cream|a5`) whenever the pick
   * changes — including once on mount, since the initial state already picks
   * an option per axis. The caller resolves this against the product's SKUs;
   * the selector itself knows nothing about pricing or stock.
   */
  onChange?: (combination: string) => void;
}) {
  /**
   * Whether some combination the shopper could still reach includes this
   * option and has stock.
   *
   * The other axes are held where they are, so on a product with two of them
   * the sizes offered are the ones that exist *in the chosen colour* rather
   * than the ones that exist in any colour. An axis the shopper has not settled
   * yet constrains nothing.
   */
  function isOffered(axisIndex: number, optionId: string, picked: Record<string, string>): boolean {
    if (skus.length === 0) return axes[axisIndex]?.options.find((o) => o.id === optionId)?.available ?? false;

    return skus.some((sku) => {
      if (sku.stock <= 0 || !sku.available) return false;

      const parts = sku.combination.split('|');
      if (parts[axisIndex] !== optionId) return false;

      // Every other axis either agrees with what is picked, or is unpicked.
      return axes.every(
        (axis, index) => index === axisIndex || !picked[axis.id] || parts[index] === picked[axis.id],
      );
    });
  }

  // Opens on something the shopper can actually buy. Picking the first option
  // that merely *exists* landed them on a sold-out size and a dead button,
  // which reads as a broken page rather than as a sold-out size.
  const [selected, setSelected] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      axes.map((axis, index) => [
        axis.id,
        axis.options.find((option) => isOffered(index, option.id, {}))?.id ??
          axis.options.find((option) => option.available)?.id ??
          '',
      ]),
    ),
  );

  useEffect(() => {
    onChange?.(axes.map((axis) => selected[axis.id] ?? '').join('|'));
    // Only the selection itself should re-fire this — `axes` is stable for
    // the page's lifetime and `onChange` is re-created each render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected]);

  return (
    <div className="flex flex-col gap-lg">
      {axes.map((axis, axisIndex) => {
        const chosen = axis.options.find((option) => option.id === selected[axis.id]);

        return (
          <fieldset key={axis.id} className="flex flex-col gap-sm">
            <legend className="mb-sm flex items-center gap-xs">
              <span className="text-label-md font-semibold text-primary">{axis.label}</span>
              {chosen && (
                <span className="text-caption text-on-surface-variant">: {chosen.label}</span>
              )}
            </legend>

            <div role="radiogroup" aria-label={axis.label} className="flex flex-wrap gap-sm">
              {axis.options.map((option) => {
                const active = selected[axis.id] === option.id;
                // The warehouse's answer, not the form's — see `isOffered`.
                const offered = isOffered(axisIndex, option.id, selected);

                if (axis.kind === 'swatch') {
                  return (
                    <button
                      key={option.id}
                      type="button"
                      role="radio"
                      aria-checked={active}
                      aria-label={option.label}
                      title={option.label}
                      disabled={!offered}
                      onClick={() => setSelected((s) => ({ ...s, [axis.id]: option.id }))}
                      className={cn(
                        'relative flex h-10 w-10 items-center justify-center rounded-full border-2 transition-colors',
                        active ? 'border-primary' : 'border-outline-variant',
                        !offered && 'cursor-not-allowed opacity-40',
                      )}
                    >
                      <span
                        className="h-7 w-7 rounded-full border border-outline-variant/40"
                        style={{ backgroundColor: option.hex }}
                      />
                      {active && (
                        <Icon
                          name="check"
                          size={18}
                          className="absolute text-on-primary mix-blend-difference"
                        />
                      )}
                    </button>
                  );
                }

                return (
                  <button
                    key={option.id}
                    type="button"
                    role="radio"
                    aria-checked={active}
                    disabled={!offered}
                    aria-label={offered ? undefined : `${option.label} — ناموجود`}
                    onClick={() => setSelected((s) => ({ ...s, [axis.id]: option.id }))}
                    className={cn(
                      'min-w-[56px] rounded-lg border px-md py-sm text-label-md font-medium transition-colors',
                      active
                        ? 'border-primary bg-soft-mint/40 text-primary'
                        : 'border-outline-variant text-on-surface hover:bg-surface-container-low',
                      !offered && 'cursor-not-allowed text-outline-variant line-through opacity-60',
                    )}
                  >
                    {option.label}
                  </button>
                );
              })}
            </div>
          </fieldset>
        );
      })}
    </div>
  );
}
