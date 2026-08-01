'use client';

import { cn } from '../lib/cn';

export interface TabItem {
  id: string;
  label: string;
  /** Optional count rendered as a superscript-ish badge, e.g. filter counts. */
  count?: number;
}

export interface TabsProps {
  items: TabItem[];
  value: string;
  onChange: (id: string) => void;
  /** `pill` matches the horizontal chip rows; `underline` the product tabs. */
  variant?: 'pill' | 'underline';
  className?: string;
}

export function Tabs({ items, value, onChange, variant = 'pill', className }: TabsProps) {
  return (
    <div
      role="tablist"
      className={cn(
        'hide-scrollbar flex gap-sm overflow-x-auto',
        variant === 'underline' && 'border-b border-outline-variant/40',
        className,
      )}
    >
      {items.map((item) => {
        const active = item.id === value;
        return (
          <button
            key={item.id}
            role="tab"
            type="button"
            aria-selected={active}
            onClick={() => onChange(item.id)}
            className={cn(
              'shrink-0 whitespace-nowrap text-label-md font-label-md transition-all',
              variant === 'pill'
                ? cn(
                    'rounded-full px-lg py-sm',
                    active
                      ? 'bg-primary-container text-on-primary'
                      : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary',
                  )
                : cn(
                    'border-b-2 px-md pb-sm pt-md',
                    active
                      ? 'border-secondary-container text-primary'
                      : 'border-transparent text-on-surface-variant hover:text-primary',
                  ),
            )}
          >
            {item.label}
          </button>
        );
      })}
    </div>
  );
}
