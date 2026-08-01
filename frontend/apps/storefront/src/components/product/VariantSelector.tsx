'use client';

import { useState } from 'react';
import { Icon, cn } from '@bojan/ui';
import type { ProductVariantAxis } from '@/lib/api/types';

/**
 * Screen 86 — colour swatches and size chips.
 * Unavailable options stay visible but disabled, so the shopper can see what
 * exists rather than wondering why a size vanished.
 */
export function VariantSelector({ axes }: { axes: ProductVariantAxis[] }) {
  const [selected, setSelected] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      axes.map((axis) => [axis.id, axis.options.find((option) => option.available)?.id ?? '']),
    ),
  );

  return (
    <div className="flex flex-col gap-lg">
      {axes.map((axis) => {
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

                if (axis.kind === 'swatch') {
                  return (
                    <button
                      key={option.id}
                      type="button"
                      role="radio"
                      aria-checked={active}
                      aria-label={option.label}
                      title={option.label}
                      disabled={!option.available}
                      onClick={() => setSelected((s) => ({ ...s, [axis.id]: option.id }))}
                      className={cn(
                        'relative flex h-10 w-10 items-center justify-center rounded-full border-2 transition-colors',
                        active ? 'border-primary' : 'border-outline-variant',
                        !option.available && 'cursor-not-allowed opacity-40',
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
                    disabled={!option.available}
                    onClick={() => setSelected((s) => ({ ...s, [axis.id]: option.id }))}
                    className={cn(
                      'min-w-[56px] rounded-lg border px-md py-sm text-label-md font-medium transition-colors',
                      active
                        ? 'border-primary bg-soft-mint/40 text-primary'
                        : 'border-outline-variant text-on-surface hover:bg-surface-container-low',
                      !option.available && 'cursor-not-allowed text-outline-variant line-through opacity-60',
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
