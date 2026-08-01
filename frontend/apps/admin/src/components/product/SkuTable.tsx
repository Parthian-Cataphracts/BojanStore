'use client';

import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Code,
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
        Which number the shop sells from is the one thing an operator on this
        screen could get wrong. The storefront reserves against the product's
        own stock; these rows are a record of what exists per combination, and
        setting one does not change what a shopper can buy. Saying so beside
        both figures is cheaper than the support ticket that follows an oversell.
      */}
      <Card className="flex items-start gap-sm border-primary/30 p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <div className="flex flex-col gap-xs">
          <p className="text-caption leading-relaxed text-on-surface-variant">
            فروشگاه از موجودی خودِ محصول کم می‌کند، نه از این جدول. موجودی SKUها یک دفتر ثبت به ازای
            هر ترکیب است و تغییر آن روی آنچه مشتری می‌تواند بخرد اثری ندارد.
          </p>
          <p className="tabular text-caption text-on-surface-variant">
            موجودی قابل فروش محصول: {toPersianDigits(productStock)} — مجموع SKUهای فعال:{' '}
            {toPersianDigits(skuUnits)}
            {skuUnits !== productStock && rows.length > 0 ? (
              <span className="text-error"> (این دو با هم نمی‌خوانند)</span>
            ) : null}
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

        {saved && (
          <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
            <Icon name="check_circle" size={16} />
            SKUها ذخیره شد.
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
