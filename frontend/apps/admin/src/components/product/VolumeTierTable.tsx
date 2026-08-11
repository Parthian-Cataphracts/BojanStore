'use client';

import { useState } from 'react';
import { Button, Card, FormStatus, Icon, IconButton, Input, formatPrice, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import type { ProductVolumeTierDto } from '@/lib/api/types';

interface Row {
  minimumQuantity: string;
  discountPercent: string;
}

/**
 * A product's volume ladder — the B2B quantity breaks.
 *
 * Rungs are floors rather than ranges: "from twenty units, ten percent". Ranges
 * have to be kept adjacent by hand and a gap between two of them is a quantity
 * with no price, which a floor cannot produce.
 *
 * The preview below is the point of the screen. A percentage is easy to type and
 * hard to picture, and the figure that matters to whoever sets it is what a
 * carton of a hundred actually comes to.
 */
export function VolumeTierTable({
  productId,
  listPrice,
  tiers,
}: {
  productId: string;
  /** The product's own price, so the preview shows money rather than percentages. */
  listPrice: number;
  tiers: ProductVolumeTierDto[];
}) {
  const [rows, setRows] = useState<Row[]>(
    tiers.map((tier) => ({
      minimumQuantity: String(tier.minimumQuantity),
      discountPercent: String(tier.discountPercent),
    })),
  );
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function update(index: number, patch: Partial<Row>) {
    setRows((current) => current.map((row, at) => (at === index ? { ...row, ...patch } : row)));
    setSaved(false);
  }

  // An empty box is not a zero. `Number('')` is 0, which is a finite number and
  // would post a rung of "from zero units, zero percent" — refused by the API,
  // but only after the operator has pressed save on a row they simply had not
  // finished typing.
  const incomplete = rows.some(
    (row) => row.minimumQuantity.trim() === '' || row.discountPercent.trim() === '',
  );

  const parsed = rows
    .filter((row) => row.minimumQuantity.trim() !== '' && row.discountPercent.trim() !== '')
    .map((row) => ({
      minimumQuantity: Number(row.minimumQuantity),
      discountPercent: Number(row.discountPercent),
    }))
    .filter((row) => Number.isFinite(row.minimumQuantity) && Number.isFinite(row.discountPercent));

  const sorted = [...parsed].sort((a, b) => a.minimumQuantity - b.minimumQuantity);

  /**
   * The same rule the API enforces, checked here so the operator sees it while
   * typing rather than after pressing save. A ladder that pays less for more is
   * a row typed on the wrong line.
   */
  const notIncreasing = sorted.some(
    (row, index) => index > 0 && row.discountPercent <= sorted[index - 1]!.discountPercent,
  );

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);

    try {
      await postJson('/api/admin/product-volume-tiers', { id: productId, tiers: sorted });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره پله‌ها انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-md p-lg">
        <div className="flex flex-col gap-xs">
          <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
            <Icon name="stacked_bar_chart" size={22} />
            پله‌های خرید عمده
          </h3>
          <p className="text-caption leading-relaxed text-on-surface-variant">
            فقط روی پیش‌فاکتورهای سازمانی اعمال می‌شود. قیمت فروشگاه برای مشتری عادی تغییری
            نمی‌کند.
          </p>
        </div>

        {rows.length === 0 ? (
          <p className="rounded-lg bg-surface-container-low p-md text-body-md leading-relaxed text-on-surface-variant">
            پله‌ای تعریف نشده — هر تعدادی که سفارش داده شود با همان قیمت فروشگاه قیمت می‌خورد.
          </p>
        ) : (
          <ul className="flex flex-col gap-md">
            {rows.map((row, index) => (
              <li key={index} className="flex flex-wrap items-end gap-sm">
                <Input
                  label="از این تعداد به بالا"
                  type="number"
                  inputMode="numeric"
                  min={2}
                  value={row.minimumQuantity}
                  onChange={(event) => update(index, { minimumQuantity: event.target.value })}
                  className="latin"
                  wrapperClassName="w-full sm:w-44"
                />

                <Input
                  label="درصد تخفیف"
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={90}
                  value={row.discountPercent}
                  onChange={(event) => update(index, { discountPercent: event.target.value })}
                  className="latin"
                  wrapperClassName="w-full sm:w-36"
                />

                <IconButton
                  icon="delete"
                  label={`حذف پله ${toPersianDigits(index + 1)}`}
                  onClick={() => {
                    setRows((current) => current.filter((_, at) => at !== index));
                    setSaved(false);
                  }}
                />
              </li>
            ))}
          </ul>
        )}

        <Button
          type="button"
          variant="outline"
          icon="add"
          className="self-start"
          onClick={() => {
            setRows((current) => [...current, { minimumQuantity: '', discountPercent: '' }]);
            setSaved(false);
          }}
        >
          افزودن پله
        </Button>
      </Card>

      {sorted.length > 0 && (
        <Card className="flex flex-col gap-md p-lg">
          <h3 className="font-headline text-card-title text-primary">پیش‌نمایش قیمت</h3>

          <div className="overflow-x-auto">
            <table className="w-full min-w-[420px] text-start">
              <thead className="text-on-surface-variant">
                <tr>
                  <th scope="col" className="py-sm text-start text-caption">تعداد</th>
                  <th scope="col" className="py-sm text-start text-caption">تخفیف</th>
                  <th scope="col" className="py-sm text-start text-caption">قیمت هر عدد</th>
                  <th scope="col" className="py-sm text-start text-caption">جمع</th>
                </tr>
              </thead>

              <tbody>
                {sorted.map((tier) => {
                  // The same arithmetic the domain does, rounded the same way —
                  // down, so the discount is never less than the ladder promises.
                  const unit = Math.floor((listPrice * (100 - tier.discountPercent)) / 100);

                  return (
                    <tr key={tier.minimumQuantity} className="border-t border-outline-variant/30">
                      <td className="tabular py-sm text-body-md text-on-surface">
                        {toPersianDigits(tier.minimumQuantity)} عدد به بالا
                      </td>
                      <td className="tabular py-sm text-body-md text-on-surface">
                        {toPersianDigits(tier.discountPercent)}٪
                      </td>
                      <td className="tabular py-sm text-body-md text-on-surface">
                        {formatPrice(unit)}
                      </td>
                      <td className="tabular py-sm text-body-md text-on-surface-variant">
                        {formatPrice(unit * tier.minimumQuantity)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      <div className="flex flex-wrap items-center gap-md">
        <Button
          type="button"
          loading={saving}
          disabled={notIncreasing || incomplete}
          onClick={save}
        >
          ذخیره پله‌ها
        </Button>

        <FormStatus
          ok={saved ? 'ذخیره شد.' : null}
          error={
            error ??
            (incomplete
              ? 'یکی از پله‌ها کامل پر نشده — هم تعداد لازم است هم درصد.'
              : notIncreasing
                ? 'هر پله باید تخفیف بیشتری از پله‌ی قبل بدهد، وگرنه سفارش بزرگ‌تر گران‌تر تمام می‌شود.'
                : null)
          }
        />
      </div>
    </div>
  );
}
