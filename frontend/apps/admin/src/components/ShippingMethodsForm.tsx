'use client';

import { useState } from 'react';
import { Button, FormStatus, Input, Switch } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { AdminShippingMethodDto } from '@/lib/api/types';

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

        <FormStatus ok={saved ? 'ذخیره شد.' : null} />

        <FormStatus error={error} />
      </div>

      <p className="text-caption leading-relaxed text-on-surface-variant">
        دست‌کم یک روش ارسال باید فعال بماند؛ با خاموش بودن همه‌ی روش‌ها، صفحه‌ی تسویه‌حساب گزینه‌ای
        برای انتخاب ندارد و هیچ سفارشی ثبت نمی‌شود.
      </p>
    </div>
  );
}
