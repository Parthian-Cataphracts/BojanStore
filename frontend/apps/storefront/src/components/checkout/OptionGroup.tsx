'use client';

import { useState, type ReactNode } from 'react';
import { Icon, cn } from '@bojan/ui';

export interface Option {
  id: string;
  title: string;
  description?: string;
  /** Right-hand value, usually a price. */
  meta?: string;
  icon?: string;
  disabled?: boolean;
  /** Extra content shown under the title, e.g. a recipient line. */
  detail?: ReactNode;
}

/**
 * Radio-card group shared by the address, shipping-method, delivery-time and
 * payment-method steps (screens 71, 73, 74, 75). Selection is local because
 * the choice is only persisted when the step is submitted.
 */
export function OptionGroup({
  name,
  options,
  defaultValue,
  onChange,
  columns = 1,
}: {
  name: string;
  options: Option[];
  defaultValue?: string;
  onChange?: (id: string) => void;
  columns?: 1 | 2 | 3;
}) {
  const [selected, setSelected] = useState(defaultValue ?? options[0]?.id ?? '');

  function pick(id: string) {
    setSelected(id);
    onChange?.(id);
  }

  return (
    <div
      role="radiogroup"
      className={cn(
        'grid gap-md',
        columns === 2 && 'sm:grid-cols-2',
        columns === 3 && 'sm:grid-cols-3',
      )}
    >
      {options.map((option) => {
        const active = selected === option.id;
        return (
          <label
            key={option.id}
            className={cn(
              'flex cursor-pointer items-start gap-sm rounded-lg border p-md transition-colors',
              active
                ? 'border-primary bg-soft-mint/30'
                : 'border-outline-variant hover:bg-surface-container-low',
              option.disabled && 'cursor-not-allowed opacity-50',
            )}
          >
            <input
              type="radio"
              name={name}
              value={option.id}
              checked={active}
              disabled={option.disabled}
              onChange={() => pick(option.id)}
              className="mt-1 h-5 w-5 shrink-0 border-outline-variant text-primary focus:ring-2 focus:ring-primary/30"
            />

            {option.icon && (
              <Icon name={option.icon} size={22} className="mt-px shrink-0 text-primary" />
            )}

            <span className="flex min-w-0 flex-1 flex-col gap-xs">
              <span className="text-body-md font-medium text-on-surface">{option.title}</span>
              {option.description && (
                <span className="text-caption leading-relaxed text-on-surface-variant">
                  {option.description}
                </span>
              )}
              {option.detail}
            </span>

            {option.meta && (
              <span className="tabular shrink-0 text-label-md font-semibold text-primary">
                {option.meta}
              </span>
            )}
          </label>
        );
      })}
    </div>
  );
}
