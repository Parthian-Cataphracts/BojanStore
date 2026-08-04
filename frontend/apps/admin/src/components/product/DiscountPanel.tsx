'use client';

import { useState } from 'react';
import { Button, Card, Icon, Input, Select, cn, formatPrice, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';
import type { AdminProductDto } from '@/lib/api/types';

/** Screen 110 — Product-level discount. */
export function DiscountPanel({ product }: { product: AdminProductDto }) {
  const BASE_PRICE = product.price;
  const [mode, setMode] = useState<'percent' | 'amount'>('percent');
  const [value, setValue] = useState('15');
  const [active, setActive] = useState(true);
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const raw = Number(normalizeDigitsInput(value) || 0);
  const discount = mode === 'percent' ? Math.round((BASE_PRICE * raw) / 100) : raw;
  const finalPrice = Math.max(0, BASE_PRICE - discount);
  const invalid = mode === 'percent' ? raw > 100 : discount > BASE_PRICE;

  // A window that ends before it starts would be saved as a discount that never
  // applies, with nothing on screen to explain why.
  const rangeInvalid = Boolean(startsAt && endsAt && endsAt < startsAt);

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson('/api/admin/product-discount', {
        id: product.id,
        percent: active && mode === 'percent' ? raw : null,
        amount: active && mode === 'amount' ? raw : null,
        // Native <input type="date"> gives yyyy-mm-dd; the API binds
        // DateTimeOffset, and empty means "no boundary" rather than epoch.
        startsAt: startsAt ? new Date(startsAt).toISOString() : null,
        endsAt: endsAt ? new Date(endsAt).toISOString() : null,
      });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تخفیف انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <FormSection title="نوع تخفیف" icon="sell">
        <div className="grid gap-md md:grid-cols-2">
          <Select
            label="روش محاسبه"
            value={mode}
            onChange={(event) => setMode(event.target.value as 'percent' | 'amount')}
          >
            <option value="percent">درصدی</option>
            <option value="amount">مبلغ ثابت</option>
          </Select>

          <Input
            label={mode === 'percent' ? 'درصد تخفیف' : 'مبلغ تخفیف (تومان)'}
            inputMode="numeric"
            value={value}
            onChange={(event) => setValue(event.target.value)}
            {...(invalid
              ? {
                  error:
                    mode === 'percent'
                      ? 'درصد تخفیف نمی‌تواند بیشتر از ۱۰۰ باشد.'
                      : 'مبلغ تخفیف نمی‌تواند از قیمت محصول بیشتر باشد.',
                }
              : null)}
          />
        </div>
      </FormSection>

      <FormSection title="بازه زمانی" icon="event">
        {/*
          Real date inputs, and actually sent. These were free-text boxes with a
          Persian-date placeholder that `save()` never read, so an operator who
          scheduled a discount got a permanent one and nothing said otherwise.
          The API takes DateTimeOffset, so the value is converted on the way out.
        */}
        <div className="grid gap-md md:grid-cols-2">
          <Input
            type="date"
            label="شروع"
            icon="calendar_today"
            value={startsAt}
            onChange={(event) => setStartsAt(event.target.value)}
            hint="خالی یعنی از همین حالا"
          />
          <Input
            type="date"
            label="پایان"
            icon="calendar_today"
            value={endsAt}
            onChange={(event) => setEndsAt(event.target.value)}
            hint="خالی یعنی بدون پایان"
            {...(rangeInvalid ? { error: 'تاریخ پایان باید بعد از شروع باشد.' } : null)}
          />
        </div>

        <div className="flex items-center justify-between gap-md">
          <span className="text-body-md text-on-surface">تخفیف فعال باشد</span>
          <button
            type="button"
            role="switch"
            aria-checked={active}
            aria-label="فعال بودن تخفیف"
            onClick={() => setActive((current) => !current)}
            className={cn(
              'relative h-6 w-11 shrink-0 rounded-full transition-colors',
              active ? 'bg-primary' : 'bg-outline-variant',
            )}
          >
            <span
              className={cn(
                'absolute top-1 h-4 w-4 rounded-full bg-surface-container-lowest transition-all',
                active ? 'start-1' : 'start-6',
              )}
            />
          </button>
        </div>
      </FormSection>

      {/* Live preview so the operator sees the customer-facing result. */}
      <Card className="flex flex-col gap-sm p-lg">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="visibility" size={22} />
          پیش‌نمایش قیمت
        </h3>

        <dl className="flex flex-col gap-sm text-body-md">
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">قیمت پایه</dt>
            <dd className="tabular text-on-surface">{formatPrice(BASE_PRICE)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">تخفیف</dt>
            <dd className="tabular text-secondary">
              −{formatPrice(invalid ? 0 : discount)}
              {mode === 'percent' && !invalid && ` (${toPersianDigits(raw)}٪)`}
            </dd>
          </div>
          <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
            <dt className="text-body-lg font-semibold text-primary">قیمت نهایی</dt>
            <dd className="tabular text-body-lg font-semibold text-primary">
              {formatPrice(invalid ? BASE_PRICE : finalPrice)}
            </dd>
          </div>
        </dl>
      </Card>

      <div className="flex items-center gap-md">
        <Button
          size="lg"
          disabled={invalid || rangeInvalid}
          loading={saving}
          onClick={save}
          className="px-xl"
        >
          ذخیره تخفیف
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
