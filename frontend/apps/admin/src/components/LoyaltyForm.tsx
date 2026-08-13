'use client';

import { useState } from 'react';
import { Button, Card, FormStatus, Icon, IconButton, Input, Switch, formatPrice, toPersianDigits } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { LoyaltyProgrammeDto, LoyaltyTierDto } from '@/lib/api/types';

interface Row {
  name: string;
  minimumPoints: string;
  discountPercent: string;
  freeShipping: boolean;
}

/** Matches `Loyalty.MaxDiscountPercent`, which the API refuses past. */
const MAX_DISCOUNT = 50;

/**
 * The loyalty club.
 *
 * Until this screen existed the club was a page on the storefront and nothing
 * else: it advertised three tiers, a permanent discount and unlimited free
 * delivery, and no order ever earned a point or had a Toman taken off. Every
 * figure here is now read by the checkout.
 *
 * The preview under each rung is the point of the screen. "Ten percent" is easy
 * to type and hard to picture; what a member has to spend to get there, and what
 * they save on a typical order, is the thing an owner is actually deciding.
 */
export function LoyaltyForm({ programme }: { programme: LoyaltyProgrammeDto }) {
  const [tomanPerPoint, setTomanPerPoint] = useState(String(programme.tomanPerPoint));
  const [rows, setRows] = useState<Row[]>(
    programme.tiers.map((tier) => ({
      name: tier.name,
      minimumPoints: String(tier.minimumPoints),
      discountPercent: String(tier.discountPercent),
      freeShipping: tier.freeShipping,
    })),
  );
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function update(index: number, patch: Partial<Row>) {
    setRows((current) => current.map((row, at) => (at === index ? { ...row, ...patch } : row)));
    setSaved(false);
  }

  const rate = Number(tomanPerPoint) || 0;

  const incomplete = rows.some(
    (row) => row.name.trim() === '' || row.minimumPoints.trim() === '' || row.discountPercent.trim() === '',
  );

  const parsed: LoyaltyTierDto[] = rows
    .filter((row) => row.name.trim() !== '' && row.minimumPoints.trim() !== '')
    .map((row) => ({
      name: row.name.trim(),
      minimumPoints: Number(row.minimumPoints) || 0,
      discountPercent: Number(row.discountPercent) || 0,
      freeShipping: row.freeShipping,
    }))
    .sort((a, b) => a.minimumPoints - b.minimumPoints);

  const duplicatePoints =
    new Set(parsed.map((tier) => tier.minimumPoints)).size !== parsed.length;

  /** The same rule the API enforces, and the B2B ladder before it. */
  const notIncreasing = parsed.some(
    (tier, index) => index > 0 && tier.discountPercent < parsed[index - 1]!.discountPercent,
  );

  const tooGenerous = parsed.some((tier) => tier.discountPercent > MAX_DISCOUNT);

  const problem = incomplete
    ? 'یکی از پله‌ها کامل پر نشده — نام، امتیاز لازم و درصد تخفیف هر سه لازم‌اند.'
    : duplicatePoints
      ? 'دو پله نمی‌توانند از یک امتیاز شروع شوند، وگرنه معلوم نیست عضو کدام را دارد.'
      : notIncreasing
        ? 'هر پله باید دست‌کم به اندازه‌ی پله‌ی قبل تخفیف بدهد، وگرنه عضو با خرج بیشتر بدتر می‌شود.'
        : tooGenerous
          ? `بیشترین تخفیف مجاز ${toPersianDigits(MAX_DISCOUNT)}٪ است.`
          : null;

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);

    try {
      await postJson('/api/admin/loyalty', { tomanPerPoint: rate, tiers: parsed });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره باشگاه مشتریان انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      {/*
        Whether the club exists at all, said out loud.

        A club with no tiers is not a club: the storefront's `/loyalty` page
        answers 404 and the footer drops its link, deliberately, so the shop
        never advertises rewards it has not defined. What was missing was any
        sign of that from here — an owner who had opened this screen, set the
        earning rate and saved had a club that was live in the panel and
        invisible to every customer, with nothing anywhere explaining the gap.
      */}
      <Card
        className={`flex items-start gap-sm p-md ${
          rows.length > 0 ? 'border-primary/40' : 'border-secondary/50'
        }`}
      >
        <Icon
          name={rows.length > 0 ? 'check_circle' : 'visibility_off'}
          size={20}
          className={`mt-px shrink-0 ${rows.length > 0 ? 'text-primary' : 'text-secondary'}`}
        />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          {rows.length > 0 ? (
            <>
              باشگاه برای مشتری‌ها <strong className="text-primary">فعال است</strong>: صفحه‌ی
              «باشگاه مشتریان» در سایت باز می‌شود و لینکش در فوتر می‌آید.
            </>
          ) : (
            <>
              تا وقتی <strong className="text-secondary">حتی یک پله</strong> تعریف نکنید، باشگاه
              برای مشتری‌ها وجود ندارد — صفحه‌اش در سایت باز نمی‌شود و لینکی هم نمایش داده
              نمی‌شود. نرخ امتیاز به‌تنهایی کافی نیست.
            </>
          )}
        </p>
      </Card>

      <FormSection title="امتیاز گرفتن" icon="savings">
        <Input
          label="هر چند تومان خرید، یک امتیاز (تومان)"
          type="number"
          inputMode="numeric"
          min={0}
          value={tomanPerPoint}
          onChange={(event) => {
            setTomanPerPoint(event.target.value);
            setSaved(false);
          }}
          className="latin"
          hint="امتیاز فقط پس از تحویل سفارش داده می‌شود، نه هنگام ثبت آن. عدد صفر یعنی باشگاه موقتاً امتیاز نمی‌دهد؛ امتیاز اعضا پاک نمی‌شود."
        />

        {rate > 0 && (
          <p className="text-caption leading-relaxed text-on-surface-variant">
            یعنی خرید {formatPrice(rate * 100)} برابر است با {toPersianDigits(100)} امتیاز.
          </p>
        )}
      </FormSection>

      {rows.length === 0 ? (
        <p className="rounded-lg bg-surface-container-low p-md text-body-md leading-relaxed text-on-surface-variant">
          پله‌ای تعریف نشده — تا وقتی پله‌ای نباشد، باشگاه مشتریان در فروشگاه نمایش داده نمی‌شود.
        </p>
      ) : (
        rows.map((row, index) => {
          const points = Number(row.minimumPoints) || 0;
          const percent = Number(row.discountPercent) || 0;

          return (
            <FormSection key={index} title={row.name || `پله ${toPersianDigits(index + 1)}`} icon="workspace_premium">
              <div className="flex flex-wrap items-end gap-sm">
                <Input
                  label="نام پله"
                  value={row.name}
                  placeholder="نقره‌ای"
                  onChange={(event) => update(index, { name: event.target.value })}
                  wrapperClassName="w-full sm:w-48"
                />

                <Input
                  label="از این امتیاز به بالا"
                  type="number"
                  inputMode="numeric"
                  min={0}
                  value={row.minimumPoints}
                  onChange={(event) => update(index, { minimumPoints: event.target.value })}
                  className="latin"
                  wrapperClassName="w-full sm:w-44"
                />

                <Input
                  label="درصد تخفیف دائمی"
                  type="number"
                  inputMode="numeric"
                  min={0}
                  max={MAX_DISCOUNT}
                  value={row.discountPercent}
                  onChange={(event) => update(index, { discountPercent: event.target.value })}
                  className="latin"
                  wrapperClassName="w-full sm:w-40"
                />

                <IconButton
                  icon="delete"
                  label={`حذف پله ${toPersianDigits(index + 1)}`}
                  onClick={() => {
                    setRows((current) => current.filter((_, at) => at !== index));
                    setSaved(false);
                  }}
                />
              </div>

              <Switch
                label="ارسال برای این پله همیشه رایگان باشد"
                checked={row.freeShipping}
                onChange={(value) => update(index, { freeShipping: value })}
              />

              {/* What the owner is actually deciding, in money. */}
              {rate > 0 && (
                <p className="tabular text-caption leading-relaxed text-on-surface-variant">
                  رسیدن به این پله: {formatPrice(points * rate)} خرید
                  {percent > 0 && <> — و بعد از آن {toPersianDigits(percent)}٪ تخفیف روی هر سفارش</>}
                </p>
              )}
            </FormSection>
          );
        })
      )}

      <Button
        type="button"
        variant="outline"
        icon="add"
        className="self-start"
        onClick={() => {
          setRows((current) => [
            ...current,
            { name: '', minimumPoints: '', discountPercent: '', freeShipping: false },
          ]);
          setSaved(false);
        }}
      >
        افزودن پله
      </Button>

      <div className="flex flex-wrap items-center gap-md">
        <Button type="button" loading={saving} disabled={problem !== null} onClick={save}>
          ذخیره باشگاه مشتریان
        </Button>

        <FormStatus ok={saved ? 'ذخیره شد.' : null} error={error ?? problem} />
      </div>

      <p className="flex items-start gap-xs text-caption leading-relaxed text-on-surface-variant">
        <Icon name="info" size={16} className="mt-2xs shrink-0" />
        تخفیف پله روی هر سفارش عضو اعمال می‌شود و با کد تخفیف جمع می‌شود: اول کد تخفیف کسر
        می‌شود، بعد درصد پله روی باقی‌مانده. هر دو جداگانه روی سفارش ثبت می‌شوند.
      </p>
    </div>
  );
}
