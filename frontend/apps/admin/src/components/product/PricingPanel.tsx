'use client';

import { useState } from 'react';
import { Button, Card, Icon, Input, formatPrice, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';
import type { AdminProductDto } from '@/lib/api/types';

const tiers = [
  { id: 't-1', from: 1, to: 9, price: 350_000 },
  { id: 't-2', from: 10, to: 49, price: 320_000 },
  { id: 't-3', from: 50, to: null, price: 290_000 },
];

/** Screen 109 — Pricing, including the B2B quantity ladder. */
export function PricingPanel({ product }: { product: AdminProductDto }) {
  const [base, setBase] = useState(String(product.price));
  const [costPrice, setCostPrice] = useState(String(product.costPrice));
  const [compareAt, setCompareAt] = useState('');
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const baseValue = Number(normalizeDigitsInput(base) || 0);
  const compareValue = Number(normalizeDigitsInput(compareAt) || 0);
  // Only a higher list price is a discount; anything else is a data error.
  const percent =
    compareValue > baseValue ? Math.round(((compareValue - baseValue) / compareValue) * 100) : 0;

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson('/api/admin/product-pricing', {
        id: product.id,
        price: baseValue,
        costPrice: Number(normalizeDigitsInput(costPrice) || 0),
        compareAtPrice: compareValue,
      });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره قیمت‌ها انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <FormSection title="قیمت پایه" icon="payments">
        <div className="grid gap-md md:grid-cols-2">
          <Input
            label="قیمت فروش (تومان)"
            inputMode="numeric"
            value={base}
            onChange={(event) => setBase(event.target.value)}
          />
          <Input
            label="قیمت پیش از تخفیف (تومان)"
            inputMode="numeric"
            hint="اگر تخفیف ندارد خالی بگذارید."
            value={compareAt}
            onChange={(event) => setCompareAt(event.target.value)}
          />
          <Input
            label="قیمت تمام‌شده (تومان)"
            inputMode="numeric"
            value={costPrice}
            onChange={(event) => setCostPrice(event.target.value)}
          />
        </div>

        {percent > 0 ? (
          <p className="flex items-center gap-xs rounded-lg bg-soft-mint px-md py-sm text-body-md text-primary">
            <Icon name="sell" size={20} />
            <span className="tabular">
              {toPersianDigits(percent)}٪ تخفیف — مشتری {formatPrice(compareValue - baseValue)} کمتر
              می‌پردازد.
            </span>
          </p>
        ) : (
          compareValue > 0 && (
            <p className="flex items-center gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container">
              <Icon name="error" size={20} />
              قیمت پیش از تخفیف باید بیشتر از قیمت فروش باشد.
            </p>
          )
        )}
      </FormSection>

      <section className="flex flex-col gap-md">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="stacked_bar_chart" size={22} />
          قیمت‌گذاری پلکانی سازمانی
        </h3>

        <DataTable
          rows={tiers}
          rowKey={(row) => row.id}
          columns={[
            {
              key: 'range',
              header: 'تعداد',
              cell: (row) =>
                row.to === null
                  ? `${toPersianDigits(row.from)} به بالا`
                  : `${toPersianDigits(row.from)} تا ${toPersianDigits(row.to)}`,
            },
            { key: 'price', header: 'قیمت هر واحد', cell: (row) => formatPrice(row.price) },
            {
              key: 'saving',
              header: 'صرفه‌جویی',
              cell: (row) =>
                row.price < 350_000 ? formatPrice(350_000 - row.price) : '—',
            },
          ]}
        />
      </section>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          قیمت پلکانی فقط روی سفارش‌های سازمانی و پیش‌فاکتورها اعمال می‌شود، نه روی سبد خرید عادی.
        </p>
      </Card>

      <div className="flex items-center gap-md">
        <Button size="lg" loading={saving} onClick={save} className="px-xl">
          ذخیره قیمت‌ها
        </Button>
        {saved && (
          <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
            <Icon name="check_circle" size={16} />
            ذخیره شد.
          </span>
        )}
        {error && (
          <span role="alert" className="flex items-center gap-xs text-caption text-error">
            <Icon name="error" size={16} />
            {error}
          </span>
        )}
      </div>
    </div>
  );
}
