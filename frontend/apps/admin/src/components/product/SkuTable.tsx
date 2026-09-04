'use client';

import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Code,
  FormStatus,
  Icon,
  Input,
  formatPrice,
  normalizeDigitsInput,
  toPersianDigits,
} from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { postJson } from '@/lib/submit';
import type { AdminSkuDto, AdminVariantAxisDto } from '@/lib/api/types';

/**
 * Screen 108 — SKU management.
 *
 * One row per sellable combination, with its own code, barcode, price and
 * stock. The whole list is posted on save and the API replaces what it holds,
 * so a deletion here is a deletion there.
 *
 * The combination is stored as option *keys* joined by `|` — renaming an
 * option's label later must not orphan the SKU that used it — and rendered
 * through the axes so the operator reads labels rather than keys.
 */
export function SkuTable({
  productId,
  skus,
  axes,
  defaultPrice,
  productStock,
}: {
  productId: string;
  skus: AdminSkuDto[];
  axes: AdminVariantAxisDto[];
  defaultPrice: number;
  /** The product's own stock — the number the storefront actually sells from. */
  productStock: number;
}) {
  const [rows, setRows] = useState<AdminSkuDto[]>(skus);
  const [code, setCode] = useState('');
  const [barcode, setBarcode] = useState('');
  const [combination, setCombination] = useState('');

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const trimmedCode = code.trim().toUpperCase();
  const duplicate = rows.some((row) => row.code.toUpperCase() === trimmedCode);

  /** Turns `cream|a5` into "کرمی گرم · A5", falling back to the raw key. */
  function describe(value: string): string {
    if (!value) return '—';

    return value
      .split('|')
      .map((key) => {
        for (const axis of axes) {
          const option = axis.options.find((candidate) => candidate.key === key);
          if (option) return option.label;
        }
        return key;
      })
      .join(' · ');
  }

  function add() {
    if (!trimmedCode || duplicate) return;

    setRows((current) => [
      ...current,
      {
        // Local only until saved; the API assigns the real id.
        id: `new-${current.length}-${trimmedCode}`,
        code: trimmedCode,
        ...(barcode.trim() ? { barcode: normalizeDigitsInput(barcode.trim()) } : null),
        combination: combination.trim(),
        price: defaultPrice,
        stock: 0,
        compareAt: null,
        active: true,
      },
    ]);

    setCode('');
    setBarcode('');
    setCombination('');
    setSaved(false);
  }

  function update(id: string, patch: Partial<AdminSkuDto>) {
    setSaved(false);
    setRows((current) => current.map((row) => (row.id === id ? { ...row, ...patch } : row)));
  }

  async function save() {
    setSaving(true);
    setError(null);
    try {
      await postJson('/api/admin/product-skus', {
        id: productId,
        skus: rows.map((row) => ({
          code: row.code,
          barcode: row.barcode ?? null,
          combination: row.combination,
          price: row.price,
          stock: row.stock,
          // Sent even though the column below is the only thing that edits it.
          // The API replaces the product's whole SKU list, so a field this
          // screen left out would come back as null — and a save here would
          // silently clear every per-size discount set on screen 107.
          compareAt: row.compareAt ?? 0,
          active: row.active,
        })),
      });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره SKUها انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  /** Every combination the axes allow, as `key|key` — the cartesian product. */
  const allowed = axes.reduce<string[]>(
    (accumulated, axis) =>
      accumulated.flatMap((prefix) =>
        axis.options
          .filter((option) => option.available)
          .map((option) => (prefix ? `${prefix}|${option.key}` : option.key)),
      ),
    [''],
  );

  const skuUnits = rows
    .filter((row) => row.active)
    .reduce((sum, row) => sum + row.stock, 0);

  return (
    <div className="flex flex-col gap-lg">
      {/*
        This used to say the opposite — that the shop sells from the product's
        own stock and these rows change nothing a shopper can buy. That has not
        been true for as long as the checkout has priced a line from its SKU:
        `CheckoutService.PriceLines` charges `sku.Price`, refuses the order when
        `sku.Stock` is short, and reduces that row on placement. An operator who
        believed the old sentence would have set a variant's price and expected
        it to be decorative.

        The two figures stay side by side, but no longer as a discrepancy: a
        product with variants sells from its combinations and the product's own
        count is what a line naming no combination draws on, so the two are
        answering different questions and have no reason to agree.
      */}
      <Card className="flex items-start gap-sm border-primary/30 p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <div className="flex flex-col gap-xs">
          <p className="text-caption leading-relaxed text-on-surface-variant">
            وقتی مشتری یک ترکیب را انتخاب می‌کند، قیمت و موجودی همان ردیف ملاک است: همان مبلغ در سبد
            خرید می‌نشیند و از موجودی همان ردیف کم می‌شود. موجودی خودِ محصول فقط برای خریدی است که
            هیچ ترکیبی انتخاب نشده باشد.
          </p>
          <p className="tabular text-caption text-on-surface-variant">
            موجودی محصول بدون ترکیب: {toPersianDigits(productStock)} — مجموع ترکیب‌های فعال:{' '}
            {toPersianDigits(skuUnits)}
          </p>
        </div>
      </Card>

      <Card className="flex flex-col gap-md p-lg">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="qr_code_2" size={22} />
          افزودن SKU
        </h3>

        <div className="grid gap-md md:grid-cols-3">
          <Input
            name="code"
            label="کد SKU"
            className="latin"
            placeholder="BZ-PLN-A5-CRM"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            {...(duplicate && trimmedCode ? { error: 'این کد قبلاً اضافه شده است.' } : null)}
          />
          <Input
            name="barcode"
            label="بارکد"
            className="latin"
            inputMode="numeric"
            value={barcode}
            onChange={(event) => setBarcode(event.target.value)}
          />

          {axes.length > 0 ? (
            <label className="flex flex-col gap-xs">
              <span className="text-label-md text-on-surface-variant">ترکیب</span>
              <select
                value={combination}
                onChange={(event) => setCombination(event.target.value)}
                className="h-12 rounded-lg border border-outline-variant bg-surface-container-lowest px-md text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                <option value="">بدون ترکیب</option>
                {allowed.map((value) => (
                  <option key={value} value={value}>
                    {describe(value)}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <Input
              name="combination"
              label="ترکیب"
              hint="ابتدا در صفحه تنوع، محورها را تعریف کنید."
              value=""
              disabled
              readOnly
            />
          )}
        </div>

        <Button
          icon="add"
          type="button"
          onClick={add}
          disabled={!trimmedCode || duplicate}
          className="self-start px-lg"
        >
          افزودن
        </Button>
      </Card>

      <DataTable
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="SKU ثبت نشده"
        emptyDescription="برای ردیابی موجودی، برای هر ترکیب یک SKU تعریف کنید."
        columns={[
          { key: 'code', header: 'کد SKU', cell: (row) => <Code>{row.code}</Code> },
          {
            key: 'barcode',
            header: 'بارکد',
            cell: (row) => (row.barcode ? <Code>{row.barcode}</Code> : '—'),
          },
          { key: 'combination', header: 'ترکیب', cell: (row) => describe(row.combination) },
          {
            key: 'stock',
            header: 'موجودی',
            cell: (row) => (
              <input
                type="text"
                inputMode="numeric"
                aria-label={`موجودی ${row.code}`}
                value={toPersianDigits(row.stock)}
                onChange={(event) => {
                  const next = Number(normalizeDigitsInput(event.target.value));
                  update(row.id, { stock: Number.isFinite(next) && next >= 0 ? next : 0 });
                }}
                className="tabular w-20 rounded border border-outline-variant bg-surface-container-lowest px-sm py-1 text-body-md text-on-surface focus:border-primary focus:outline-none"
              />
            ),
          },
          {
            key: 'price',
            header: 'قیمت',
            cell: (row) => (
              <input
                type="text"
                inputMode="numeric"
                aria-label={`قیمت ${row.code}`}
                value={toPersianDigits(row.price)}
                onChange={(event) => {
                  const next = Number(normalizeDigitsInput(event.target.value));
                  update(row.id, { price: Number.isFinite(next) && next >= 0 ? next : 0 });
                }}
                title={formatPrice(row.price)}
                className="tabular w-28 rounded border border-outline-variant bg-surface-container-lowest px-sm py-1 text-body-md text-on-surface focus:border-primary focus:outline-none"
              />
            ),
          },
          {
            /*
              This combination's own discount, editable here as well as on
              screen 107 because both screens write the same row and a figure
              that could only be set in one place would look wrong in the other.
              Empty means not on sale.
            */
            key: 'compareAt',
            header: 'قیمت پیش از تخفیف',
            cell: (row) => (
              <input
                type="text"
                inputMode="numeric"
                aria-label={`قیمت پیش از تخفیف ${row.code}`}
                placeholder="بدون تخفیف"
                value={row.compareAt ? toPersianDigits(row.compareAt) : ''}
                onChange={(event) => {
                  const next = Number(normalizeDigitsInput(event.target.value));
                  update(row.id, {
                    compareAt: Number.isFinite(next) && next > 0 ? next : null,
                  });
                }}
                className="tabular w-28 rounded border border-outline-variant bg-surface-container-lowest px-sm py-1 text-body-md text-on-surface focus:border-primary focus:outline-none"
              />
            ),
          },
          {
            key: 'active',
            header: 'وضعیت',
            cell: (row) => (
              <button
                type="button"
                onClick={() => update(row.id, { active: !row.active })}
                aria-label={`تغییر وضعیت ${row.code}`}
              >
                {row.active ? (
                  <Badge tone="mint">فعال</Badge>
                ) : (
                  <Badge tone="neutral">غیرفعال</Badge>
                )}
              </button>
            ),
          },
        ]}
        actions={(row) => (
          <button
            type="button"
            aria-label={`حذف ${row.code}`}
            onClick={() => {
              setSaved(false);
              setRows((current) => current.filter((item) => item.id !== row.id));
            }}
            className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
          >
            <Icon name="delete" size={18} />
          </button>
        )}
      />

      <div className="flex flex-wrap items-center gap-md">
        <Button size="lg" loading={saving} onClick={save} className="self-start px-xl">
          ذخیره SKUها
        </Button>

        <FormStatus ok={saved ? 'SKUها ذخیره شد.' : null} />

        <FormStatus error={error} />
      </div>
    </div>
  );
}
