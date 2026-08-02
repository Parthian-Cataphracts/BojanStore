'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Icon, Input, cn, normalizeDigitsInput } from '@bojan/ui';
import { FormLayout, FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { AdminCouponDto } from '@/lib/api/types';

type DiscountKind = 'percent' | 'amount';
type Errors = Partial<Record<'code' | 'value' | 'form', string>>;

/**
 * Screen 128 — the coupon editor.
 *
 * A separate component rather than another `EntityForm` resource: the write
 * endpoint (`SaveCouponAsync`) binds `percent`/`amount`/`minimumSpend` as
 * numbers and `expiresAt` as a date, and the API's JSON deserializer does not
 * coerce strings into those types — `EntityForm`'s generic FormData-as-strings
 * submit would 400 on every save. This one converts before posting, the way
 * `ProductForm` already does for price/stock.
 */
export function CouponForm({ coupon }: { coupon?: AdminCouponDto }) {
  const router = useRouter();
  const isEdit = Boolean(coupon);
  const [discountKind, setDiscountKind] = useState<DiscountKind>(
    coupon?.amount ? 'amount' : 'percent',
  );
  // Coupon.IsActive maps to the literal string "active" — see SaveCouponAsync.
  const [active, setActive] = useState(coupon?.active ?? true);
  const [errors, setErrors] = useState<Errors>({});
  const error = errors.form;
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const code = String(data.get('code') ?? '').trim().toUpperCase();
    const value = Number(normalizeDigitsInput(String(data.get('value') ?? '')));
    const minimumSpendRaw = normalizeDigitsInput(String(data.get('minimumSpend') ?? ''));
    const expiresAtRaw = String(data.get('expiresAt') ?? '').trim();

    if (!/^[A-Z0-9-]{3,}$/.test(code)) next.code = 'کد باید انگلیسی و حداقل ۳ نویسه باشد.';
    if (!Number.isFinite(value) || value <= 0) next.value = 'مقدار تخفیف را وارد کنید.';
    if (discountKind === 'percent' && value >= 100) next.value = 'درصد تخفیف باید کمتر از ۱۰۰ باشد.';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    setSaved(false);
    try {
      await postJson('/api/admin/coupons', {
        ...(coupon ? { id: coupon.id } : null),
        code,
        percent: discountKind === 'percent' ? value : null,
        amount: discountKind === 'amount' ? value : null,
        minimumSpend: minimumSpendRaw ? Number(minimumSpendRaw) : null,
        // Native <input type="date"> already gives yyyy-mm-dd; empty clears it.
        expiresAt: expiresAtRaw ? new Date(expiresAtRaw).toISOString() : null,
        status: active ? 'active' : 'inactive',
      });
      setSaved(true);
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ذخیره کد تخفیف انجام نشد.' });
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate>
      <FormLayout
        aside={
          <FormSection title="وضعیت" icon="visibility">
            <button
              type="button"
              onClick={() => setActive((value) => !value)}
              className={cn(
                'flex items-center gap-sm rounded-lg border p-sm text-start transition-colors',
                active ? 'border-primary bg-soft-mint/40' : 'border-outline-variant hover:bg-surface-container-low',
              )}
            >
              <Icon
                name={active ? 'toggle_on' : 'toggle_off'}
                size={22}
                className={active ? 'text-primary' : 'text-outline-variant'}
              />
              <span className="text-body-md text-on-surface">{active ? 'فعال' : 'غیرفعال'}</span>
            </button>
          </FormSection>
        }
        actions={
          <>
            <Button type="submit" size="lg" loading={saving} className="px-xl">
              {isEdit ? 'ذخیره تغییرات' : 'ثبت کد تخفیف'}
            </Button>
            {/* Back to the list — the button did nothing, so "انصراف" left the
                operator on the form with no way out but the browser. */}
            <Button
              type="button"
              variant="ghost"
              size="lg"
              onClick={() => router.push('/coupons')}
              className="px-xl"
            >
              انصراف
            </Button>
            {error && (
              <span role="alert" className="flex items-center gap-xs text-caption text-error">
                <Icon name="error" size={16} />
                {error}
              </span>
            )}
            {saved && (
              <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
                <Icon name="check_circle" size={16} />
                تغییرات ذخیره شد.
              </span>
            )}
          </>
        }
      >
        <FormSection title="اطلاعات کد تخفیف" icon="local_offer">
          <Input
            name="code"
            label="کد تخفیف"
            className="latin"
            placeholder="BOJAN20"
            defaultValue={coupon?.code ?? ''}
            required
            {...(errors.code ? { error: errors.code } : null)}
          />

          <div className="flex gap-sm">
            {(['percent', 'amount'] as const).map((kind) => (
              <button
                key={kind}
                type="button"
                onClick={() => setDiscountKind(kind)}
                className={cn(
                  'flex-1 rounded-lg border p-sm text-center text-label-md transition-colors',
                  discountKind === kind
                    ? 'border-primary bg-soft-mint/40 text-primary'
                    : 'border-outline-variant text-on-surface-variant hover:bg-surface-container-low',
                )}
              >
                {kind === 'percent' ? 'درصدی' : 'مبلغ ثابت'}
              </button>
            ))}
          </div>

          <Input
            name="value"
            label={discountKind === 'percent' ? 'درصد تخفیف' : 'مبلغ تخفیف'}
            inputMode="numeric"
            suffix={discountKind === 'percent' ? '٪' : 'تومان'}
            defaultValue={coupon?.percent ?? coupon?.amount ?? ''}
            required
            {...(errors.value ? { error: errors.value } : null)}
          />

          <Input
            name="minimumSpend"
            label="حداقل مبلغ سبد خرید"
            inputMode="numeric"
            suffix="تومان"
            defaultValue={coupon?.minimumSpend ?? ''}
          />

          <Input
            name="expiresAt"
            type="date"
            label="تاریخ انقضا"
            defaultValue={coupon?.expiresAt ? coupon.expiresAt.slice(0, 10) : ''}
            hint="خالی یعنی بدون انقضا"
          />
        </FormSection>
      </FormLayout>
    </form>
  );
}
