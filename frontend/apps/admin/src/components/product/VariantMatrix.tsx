'use client';

import { useMemo, useState } from 'react';
import { Badge, Button, Card, Code, Icon, cn, formatPrice, toPersianDigits } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';

const axes = [
  { id: 'color', label: 'رنگ', options: ['کرمی', 'سبزآبی', 'مرجانی'] },
  { id: 'size', label: 'سایز', options: ['A5', 'A4'] },
];

const basePrice = 350_000;

/**
 * Screen 107 — Variant management.
 *
 * The matrix is the cartesian product of the selected axis options, so turning
 * an option off removes exactly the combinations that used it — no orphan rows.
 */
export function VariantMatrix() {
  const [enabled, setEnabled] = useState<Record<string, string[]>>({
    color: ['کرمی', 'سبزآبی'],
    size: ['A5', 'A4'],
  });

  const combinations = useMemo(() => {
    const colors = enabled.color ?? [];
    const sizes = enabled.size ?? [];

    return colors.flatMap((color) =>
      sizes.map((size) => ({
        id: `${color}-${size}`,
        color,
        size,
        sku: `BZ-PLN-${size}-${color === 'کرمی' ? 'CRM' : color === 'سبزآبی' ? 'TEA' : 'COR'}`,
        price: size === 'A4' ? basePrice + 70_000 : basePrice,
        stock: size === 'A4' ? 8 : 24,
      })),
    );
  }, [enabled]);

  function toggle(axisId: string, option: string) {
    setEnabled((current) => {
      const list = current[axisId] ?? [];
      return {
        ...current,
        [axisId]: list.includes(option)
          ? list.filter((item) => item !== option)
          : [...list, option],
      };
    });
  }

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-lg p-lg">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="tune" size={22} />
          محورهای تنوع
        </h3>

        {axes.map((axis) => (
          <fieldset key={axis.id} className="flex flex-col gap-sm">
            <legend className="mb-sm text-label-md font-medium text-on-surface-variant">
              {axis.label}
            </legend>
            <div className="flex flex-wrap gap-sm">
              {axis.options.map((option) => {
                const on = (enabled[axis.id] ?? []).includes(option);
                return (
                  <button
                    key={option}
                    type="button"
                    aria-pressed={on}
                    onClick={() => toggle(axis.id, option)}
                    className={cn(
                      'rounded-full border px-md py-sm text-label-md font-medium transition-colors',
                      on
                        ? 'border-primary bg-soft-mint/40 text-primary'
                        : 'border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
                    )}
                  >
                    {option}
                  </button>
                );
              })}
            </div>
          </fieldset>
        ))}

        <p className="tabular text-caption text-on-surface-variant">
          {toPersianDigits(combinations.length)} ترکیب ساخته می‌شود.
        </p>
      </Card>

      <DataTable
        rows={combinations}
        rowKey={(row) => row.id}
        emptyTitle="ترکیبی ساخته نشد"
        emptyDescription="حداقل یک گزینه از هر محور را فعال کنید."
        columns={[
          {
            key: 'combo',
            header: 'ترکیب',
            cell: (row) => `${row.color} · ${row.size}`,
          },
          { key: 'sku', header: 'SKU', cell: (row) => <Code>{row.sku}</Code> },
          { key: 'price', header: 'قیمت', cell: (row) => formatPrice(row.price) },
          {
            key: 'stock',
            header: 'موجودی',
            cell: (row) =>
              row.stock > 10 ? (
                <span className="tabular">{toPersianDigits(row.stock)}</span>
              ) : (
                <Badge tone="warning">{toPersianDigits(row.stock)}</Badge>
              ),
          },
        ]}
      />

      {/*
        The axes are readable (`GET /products/{slug}/variants`) but nothing
        writes them — there is no admin endpoint and no `resources.ts` entry, so
        this button had nowhere to post. The matrix below it is still worth
        keeping interactive: it is a preview of what the combinations would be,
        and it is honest as long as saving does not claim to work.
      */}
      <Button
        size="lg"
        disabled
        title="ذخیره ترکیب‌های محصول هنوز در سرور پیاده‌سازی نشده است."
        className="self-start px-xl"
      >
        ذخیره ترکیب‌ها
      </Button>
    </div>
  );
}
