'use client';

import { useState } from 'react';
import { Button, FormStatus, Icon, Input, Switch } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import { STORE_SECTION, TRUST_SEALS_KEY, type TrustSeal } from '@/lib/trust-seals';

/**
 * How many marks the footer will carry. The bar wraps, so this is not a layout
 * limit — it is that a row of twenty badges is not a claim anybody reads. The
 * storefront enforces the same ceiling on the way out.
 */
const MAX_SEALS = 8;

const blank: TrustSeal = { title: '', subtitle: '', link: '', enabled: true };

/**
 * The footer's trust marks — Enamad, a payment licence, a membership.
 *
 * Not `SettingsForm`: that one renders a fixed list of named fields and posts
 * them, and this is a list whose length the owner decides. The whole list is
 * submitted on every save, so a mark removed here is a mark gone rather than a
 * numbered key left behind for somebody to wonder about later.
 *
 * Stored as one JSON row under `store/trustSeals`, which is why the value is
 * stringified here rather than spread across `trustSeal1Title` and friends —
 * there is no sensible answer to what `trustSeal3` means once there are two.
 */
export function TrustSealsForm({ initial }: { initial: TrustSeal[] }) {
  const [seals, setSeals] = useState<TrustSeal[]>(initial);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function update(index: number, patch: Partial<TrustSeal>) {
    setSaved(false);
    setSeals((rows) => rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function remove(index: number) {
    setSaved(false);
    setSeals((rows) => rows.filter((_, i) => i !== index));
  }

  async function save() {
    // A row the owner added and never filled in is abandoned, not a claim, so
    // it is dropped rather than held against them on save.
    const filled = seals.filter(
      (seal) => seal.title.trim() || seal.subtitle.trim() || seal.link.trim(),
    );

    if (filled.some((seal) => !seal.title.trim())) {
      setError('هر نماد باید عنوان داشته باشد.');
      return;
    }

    const cleaned = filled.map((seal) => ({
      title: seal.title.trim(),
      subtitle: seal.subtitle.trim(),
      // The owner pastes whatever they have to hand. A bare domain is what a
      // registrar's panel gives you, and storing it without a scheme makes an
      // anchor the browser resolves against this site — so the badge would
      // link into the shop instead of out to the certificate.
      link: withScheme(seal.link.trim()),
      enabled: seal.enabled,
    }));

    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson('/api/admin/settings', {
        section: STORE_SECTION,
        values: { [TRUST_SEALS_KEY]: JSON.stringify(cleaned) },
      });
      setSeals(cleaned);
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره نمادها انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <FormSection
      // Matches `SettingsForm`, which sits directly above this on screen 141.
      className="max-w-4xl"
      title="نمادهای اعتماد"
      icon="verified"
      description="در نوار پایین فوتر نمایش داده می‌شوند. نمادی که خاموش باشد ذخیره می‌ماند ولی چاپ نمی‌شود — برای وقتی که مجوزی در حال تمدید است."
    >
      {seals.length === 0 && (
        <p className="text-body-sm text-on-surface-variant">
          هنوز نمادی ثبت نشده است. تا وقتی چیزی اضافه نشود، نوار پایین فوتر فقط حق نشر را نشان
          می‌دهد.
        </p>
      )}

      {seals.map((seal, index) => (
        <div
          key={index}
          className="gap-md border-outline-variant p-md flex flex-col rounded-xl border"
        >
          <div className="gap-md grid sm:grid-cols-2">
            <Input
              label="عنوان"
              value={seal.title}
              onChange={(event) => update(index, { title: event.target.value })}
              hint="مثلاً «نماد اعتماد الکترونیکی»."
              required
            />
            <Input
              label="زیرنویس"
              value={seal.subtitle}
              onChange={(event) => update(index, { subtitle: event.target.value })}
              hint="اختیاری — مثلاً شماره‌ی مجوز."
            />
          </div>

          <Input
            label="پیوند"
            value={seal.link}
            onChange={(event) => update(index, { link: event.target.value })}
            className="latin"
            dir="ltr"
            hint="اختیاری. صفحه‌ی تأیید همان نماد. خالی بگذارید تا فقط متن نمایش داده شود."
          />

          <div className="gap-md flex flex-wrap items-center justify-between">
            <Switch
              label="نمایش در فوتر"
              checked={seal.enabled}
              onChange={(next) => update(index, { enabled: next })}
            />

            <Button
              type="button"
              variant="outline"
              size="sm"
              icon="delete"
              onClick={() => remove(index)}
            >
              حذف
            </Button>
          </div>
        </div>
      ))}

      <div className="gap-md flex flex-wrap items-center">
        <Button
          type="button"
          variant="outline"
          icon="add"
          disabled={seals.length >= MAX_SEALS}
          onClick={() => {
            setSaved(false);
            setSeals((rows) => [...rows, { ...blank }]);
          }}
        >
          افزودن نماد
        </Button>

        <Button type="button" icon="verified" loading={saving} onClick={save}>
          ذخیره نمادها
        </Button>

        {seals.length >= MAX_SEALS && (
          <span className="text-caption text-on-surface-variant">
            بیشتر از {MAX_SEALS} نماد پذیرفته نمی‌شود.
          </span>
        )}
      </div>

      <FormStatus ok={saved ? 'نمادها ذخیره شد.' : null} error={error} />

      {seals.some((seal) => seal.title.trim() && !seal.enabled) && (
        <p className="gap-xs text-caption text-on-surface-variant flex items-start">
          <Icon name="visibility_off" size={16} className="mt-2xs shrink-0" />
          نمادهای خاموش ذخیره می‌شوند ولی در سایت دیده نمی‌شوند.
        </p>
      )}
    </FormSection>
  );
}

/**
 * A pasted address as an absolute one, or empty.
 *
 * Left alone when it already names a scheme, so an owner who pasted the full
 * URL gets exactly what they pasted.
 */
function withScheme(link: string): string {
  if (link.length === 0) return '';
  return /^https?:\/\//i.test(link) ? link : `https://${link}`;
}
