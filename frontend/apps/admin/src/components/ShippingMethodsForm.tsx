'use client';

import { useState } from 'react';
import { Button, Icon, Input } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { AdminShippingMethodDto } from '@/lib/api/types';

/** Toggle styled like the one the other settings screens use. */
function Switch({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-md">
      <span className="text-body-md text-on-surface">{label}</span>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={label}
        onClick={() => onChange(!checked)}
        className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
          checked ? 'bg-primary' : 'bg-outline-variant'
        }`}
      >
        <span
          className={`absolute top-1 h-4 w-4 rounded-full bg-surface-container-lowest transition-all ${
            checked ? 'start-6' : 'start-1'
          }`}
        />
      </button>
    </div>
  );
}

/**
 * The shipping tiers the checkout actually charges from.
 *
 * This screen used to write three prices into the generic settings table, which
 * nothing read — the figures the shopper was charged came from rows only the
 * seeder had ever written, so a shop whose courier put its prices up had to be
 * redeployed to follow. These fields are those rows.
 *
 * Tiers are edited rather than added and removed: the checkout screens name the
 * codes as constants, so the set is a change to both sides of the app rather
 * than something a settings form should be able to do on its own.
 */
export function ShippingMethodsForm({ methods }: { methods: AdminShippingMethodDto[] }) {
  const [rows, setRows] = useState(methods);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function update(code: string, patch: Partial<AdminShippingMethodDto>) {
    setRows((current) =>
      current.map((row) => (row.code === code ? { ...row, ...patch } : row)),
    );
    setSaved(false);
  }

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);

    try {
      await postJson('/api/admin/shipping-methods', { methods: rows });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تنظیمات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  if (rows.length === 0) {
    return (
      <p className="rounded-lg bg-surface-container-low p-lg text-body-md leading-relaxed text-on-surface-variant">
        هیچ روش ارسالی در پایگاه داده ثبت نشده است. تا وقتی حداقل یک روش وجود نداشته باشد، مشتری
        نمی‌تواند سفارشی ثبت کند.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-lg">
      {rows.map((row) => (
        <FormSection key={row.code} title={row.title || row.code} icon="local_shipping">
          <Input
            label="عنوان"
            value={row.title}
            onChange={(event) => update(row.code, { title: event.target.value })}
          />

          <Input
            label="هزینه ارسال"
            type="number"
            inputMode="numeric"
            min={0}
            value={String(row.price)}
            onChange={(event) => update(row.code, { price: Number(event.target.value) || 0 })}
            className="latin"
            hint="به تومان. عدد صفر یعنی ارسال رایگان."
          />

          <Input
            label="زمان تحویل"
            value={row.estimate}
            placeholder="۲ تا ۳ روز کاری"
            onChange={(event) => update(row.code, { estimate: event.target.value })}
            hint="متنی که کنار این گزینه در صفحه‌ی تسویه‌حساب نوشته می‌شود."
          />

          <Switch
            label="در تسویه‌حساب نمایش داده شود"
            checked={row.isActive}
            onChange={(value) => update(row.code, { isActive: value })}
          />
        </FormSection>
      ))}

      <div className="flex flex-wrap items-center gap-md">
        <Button type="button" loading={saving} onClick={save}>
          ذخیره تنظیمات
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

      <p className="text-caption leading-relaxed text-on-surface-variant">
        دست‌کم یک روش ارسال باید فعال بماند؛ با خاموش بودن همه‌ی روش‌ها، صفحه‌ی تسویه‌حساب گزینه‌ای
        برای انتخاب ندارد و هیچ سفارشی ثبت نمی‌شود.
      </p>
    </div>
  );
}
