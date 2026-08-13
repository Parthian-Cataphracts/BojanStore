'use client';

import { useRouter } from 'next/navigation';
import { useMemo, useState } from 'react';
import {
  Button,
  Card,
  FormStatus,
  Icon,
  IconButton,
  Input,
  JalaliDateInput,
  Select,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { postJson } from '@/lib/submit';
import type { AdminQuotableProductDto, ProductVolumeTierDto } from '@/lib/api/types';

interface Line {
  productId: string;
  quantity: string;
  /** Empty means "let the ladder decide", which is the normal case. */
  unitPriceOverride: string;
}

/**
 * The discount a quantity earns — the same reading the domain does.
 *
 * The highest rung the quantity reaches wins, floors rather than ranges, so a
 * ladder given in any order still has one answer.
 */
function discountPercentFor(tiers: ProductVolumeTierDto[], quantity: number): number {
  let percent = 0;

  for (const tier of tiers) {
    if (quantity >= tier.minimumQuantity && tier.discountPercent > percent) {
      percent = tier.discountPercent;
    }
  }

  return Math.min(percent, 100);
}

/** Rounded down, exactly as the server rounds it, so the preview cannot be a rial out. */
function unitPriceFor(listPrice: number, tiers: ProductVolumeTierDto[], quantity: number): number {
  return Math.floor((listPrice * (100 - discountPercentFor(tiers, quantity))) / 100);
}

/**
 * Turns a business request into a priced pro-forma.
 *
 * Before this the panel's "صدور پیش‌فاکتور" button only flipped the request's
 * status: it told the organisation a quote existed and produced no quote. The
 * document, its lines, the customer's screen for reading one and the e-mail
 * announcing it were all built and all waiting on somebody to name products and
 * quantities.
 *
 * Every figure shown here is computed the way the server computes it, and the
 * server computes them all again on submit. The preview is a courtesy to the rep
 * — the prices that reach the document come from the catalogue and each
 * product's own volume ladder, never from this form.
 */
export function QuoteComposer({
  requestId,
  requestCode,
  products,
  /** Whether a quote already exists — the request has moved past `submitted`. */
  alreadyQuoted,
}: {
  requestId: string;
  requestCode: string;
  products: AdminQuotableProductDto[];
  alreadyQuoted: boolean;
}) {
  const router = useRouter();

  const [lines, setLines] = useState<Line[]>([]);
  const [discount, setDiscount] = useState('0');
  const [taxRatePercent, setTaxRatePercent] = useState('9');
  const [validUntil, setValidUntil] = useState('');
  const [saving, setSaving] = useState(false);
  const [issued, setIssued] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const byId = useMemo(
    () => new Map(products.map((product) => [product.id, product])),
    [products],
  );

  function update(index: number, patch: Partial<Line>) {
    setLines((current) => current.map((line, at) => (at === index ? { ...line, ...patch } : line)));
    setIssued(false);
  }

  const priced = lines.map((line) => {
    const product = byId.get(line.productId);
    const quantity = Math.trunc(Number(line.quantity));
    const override = line.unitPriceOverride.trim() === '' ? null : Number(line.unitPriceOverride);

    const valid =
      product !== undefined &&
      Number.isFinite(quantity) &&
      quantity >= 1 &&
      (override === null || (Number.isFinite(override) && override >= 0));

    const unitPrice = !product
      ? 0
      : override !== null && Number.isFinite(override) && override >= 0
        ? override
        : unitPriceFor(product.price, product.tiers, Math.max(quantity, 1));

    return {
      product,
      quantity,
      override,
      valid,
      unitPrice,
      lineTotal: valid ? unitPrice * quantity : 0,
      /** What the ladder gave, so an overridden line can be read against it. */
      ladderPercent: product ? discountPercentFor(product.tiers, Math.max(quantity, 1)) : 0,
    };
  });

  const subtotal = priced.reduce((sum, line) => sum + line.lineTotal, 0);
  const flatDiscount = Math.max(0, Math.trunc(Number(discount) || 0));
  const taxRate = Math.min(100, Math.max(0, Math.trunc(Number(taxRatePercent) || 0)));

  // The order the document totals in: the flat discount comes off the lines
  // first, and tax is charged on what is left rather than on what was asked.
  const afterDiscount = Math.max(0, subtotal - flatDiscount);
  const tax = Math.floor((afterDiscount * taxRate) / 100);
  const total = afterDiscount + tax;

  const ready = priced.length > 0 && priced.every((line) => line.valid);

  async function issue() {
    setSaving(true);
    setIssued(false);
    setError(null);

    try {
      await postJson('/api/admin/business-request-quote', {
        requestId,
        lines: priced.map((line) => ({
          productId: line.product!.id,
          quantity: line.quantity,
          unitPriceOverride: line.override,
        })),
        discount: flatDiscount,
        taxRatePercent: taxRate,
        // A date the operator did not set is left out entirely, so the server's
        // own default validity applies instead of a date this form invented.
        ...(validUntil ? { validUntilUtc: new Date(validUntil).toISOString() } : null),
      });

      setIssued(true);
      setLines([]);
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'صدور پیش‌فاکتور انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      {alreadyQuoted && (
        <p className="flex items-start gap-xs rounded-lg bg-surface-container-low px-md py-sm text-body-md leading-relaxed text-on-surface-variant">
          <Icon name="info" size={20} className="mt-px shrink-0" />
          برای این درخواست قبلاً پیش‌فاکتور صادر شده است. صدور دوباره، پیش‌فاکتور تازه‌ای با
          شماره‌ی جدید می‌سازد و پیش‌فاکتور قبلی باطل نمی‌شود.
        </p>
      )}

      {products.length === 0 ? (
        <p className="rounded-lg bg-surface-container-low p-md text-body-md leading-relaxed text-on-surface-variant">
          محصول منتشرشده‌ای برای قیمت‌گذاری وجود ندارد.
        </p>
      ) : (
        <>
          {lines.length === 0 ? (
            <p className="rounded-lg bg-surface-container-low p-md text-body-md leading-relaxed text-on-surface-variant">
              هنوز قلمی اضافه نشده. هر قلم را که اضافه کنید، قیمت واحد از روی پله‌های خرید عمده‌ی
              همان محصول حساب می‌شود.
            </p>
          ) : (
            <ul className="flex flex-col gap-md">
              {lines.map((line, index) => {
                const row = priced[index]!;

                return (
                  <li key={index}>
                    <Card className="flex flex-col gap-md p-md">
                      <div className="flex flex-wrap items-end gap-sm">
                        <Select
                          label="محصول"
                          value={line.productId}
                          onChange={(event) => update(index, { productId: event.target.value })}
                          wrapperClassName="w-full sm:min-w-56 sm:flex-1"
                        >
                          <option value="">انتخاب کنید…</option>
                          {products.map((product) => (
                            <option key={product.id} value={product.id}>
                              {product.title}
                            </option>
                          ))}
                        </Select>

                        <Input
                          label="تعداد"
                          type="number"
                          inputMode="numeric"
                          min={1}
                          value={line.quantity}
                          onChange={(event) => update(index, { quantity: event.target.value })}
                          className="latin"
                          wrapperClassName="w-full sm:w-28"
                        />

                        <Input
                          label="قیمت واحد توافقی"
                          type="number"
                          inputMode="numeric"
                          min={0}
                          placeholder="از روی پله‌ها"
                          value={line.unitPriceOverride}
                          onChange={(event) =>
                            update(index, { unitPriceOverride: event.target.value })
                          }
                          className="latin"
                          wrapperClassName="w-full sm:w-44"
                        />

                        <IconButton
                          icon="delete"
                          label={`حذف قلم ${toPersianDigits(index + 1)}`}
                          onClick={() => {
                            setLines((current) => current.filter((_, at) => at !== index));
                            setIssued(false);
                          }}
                        />
                      </div>

                      {row.product && row.valid && (
                        <p className="tabular flex flex-wrap items-center gap-x-md gap-y-2xs text-caption text-on-surface-variant">
                          <span>قیمت فروشگاه: {formatPrice(row.product.price)}</span>

                          {row.override !== null ? (
                            <span className="text-tertiary">قیمت دستی</span>
                          ) : row.ladderPercent > 0 ? (
                            <span className="text-primary">
                              پله‌ی {toPersianDigits(row.ladderPercent)}٪
                            </span>
                          ) : (
                            <span>بدون تخفیف پلکانی</span>
                          )}

                          <span>واحد: {formatPrice(row.unitPrice)}</span>
                          <span className="text-on-surface">جمع: {formatPrice(row.lineTotal)}</span>
                        </p>
                      )}
                    </Card>
                  </li>
                );
              })}
            </ul>
          )}

          <Button
            type="button"
            variant="outline"
            icon="add"
            className="self-start"
            onClick={() => {
              setLines((current) => [
                ...current,
                { productId: '', quantity: '1', unitPriceOverride: '' },
              ]);
              setIssued(false);
            }}
          >
            افزودن قلم
          </Button>

          <Card className="flex flex-col gap-md p-lg">
            <div className="flex flex-wrap items-end gap-sm">
              <Input
                label="تخفیف کلی (تومان)"
                type="number"
                inputMode="numeric"
                min={0}
                value={discount}
                onChange={(event) => {
                  setDiscount(event.target.value);
                  setIssued(false);
                }}
                className="latin"
                wrapperClassName="w-full sm:w-48"
              />

              <Input
                label="مالیات (٪)"
                type="number"
                inputMode="numeric"
                min={0}
                max={100}
                value={taxRatePercent}
                onChange={(event) => {
                  setTaxRatePercent(event.target.value);
                  setIssued(false);
                }}
                className="latin"
                wrapperClassName="w-full sm:w-32"
              />

              <JalaliDateInput
                label="اعتبار تا"
                value={validUntil}
                onChange={(next) => {
                  setValidUntil(next);
                  setIssued(false);
                }}
                yearsBack={1}
                yearsAhead={3}
                hint="خالی بماند: دو هفته"
                wrapperClassName="w-full sm:w-48"
              />
            </div>

            <dl className="tabular flex flex-col gap-sm">
              {[
                { label: 'جمع اقلام', value: subtotal },
                { label: 'تخفیف کلی', value: -flatDiscount },
                { label: `مالیات ${toPersianDigits(taxRate)}٪`, value: tax },
              ].map((row) => (
                <div key={row.label} className="flex items-center justify-between gap-md">
                  <dt className="text-caption text-on-surface-variant">{row.label}</dt>
                  <dd className="text-body-md text-on-surface">{formatPrice(row.value)}</dd>
                </div>
              ))}

              <div className="flex items-center justify-between gap-md border-t border-outline-variant/40 pt-sm">
                <dt className="text-body-md font-medium text-on-surface">مبلغ نهایی</dt>
                <dd className="text-body-lg font-semibold text-primary">{formatPrice(total)}</dd>
              </div>
            </dl>
          </Card>

          <div className="flex flex-wrap items-center gap-md">
            <Button type="button" icon="request_quote" loading={saving} disabled={!ready} onClick={issue}>
              صدور پیش‌فاکتور
            </Button>

            <FormStatus
              ok={issued ? `پیش‌فاکتور درخواست ${requestCode} صادر و ایمیل شد.` : null}
              error={error}
            />
          </div>
        </>
      )}
    </div>
  );
}
