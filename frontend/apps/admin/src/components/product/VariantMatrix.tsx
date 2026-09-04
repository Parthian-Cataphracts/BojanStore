'use client';

import { useMemo, useState } from 'react';
import {
  Badge,
  Button,
  Card,
  FormStatus,
  Icon,
  Input,
  Select,
  cn,
  formatPrice,
  normalizeDigitsInput,
  toPersianDigits,
} from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { postJson } from '@/lib/submit';
import type { AdminSkuDto, AdminVariantAxisDto, AdminVariantOptionDto } from '@/lib/api/types';

type Kind = AdminVariantAxisDto['kind'];

/**
 * What one combination is worth, as this screen edits it.
 *
 * `compareAt` is the combination's own discount and zero means "not on sale" —
 * the API stores that as null. It is per combination and not per product on
 * purpose: reducing size 2 must leave size 4 at its own price, which is only
 * true because the pair lives on the row the checkout prices the line from.
 */
interface Cell {
  price: number;
  stock: number;
  compareAt: number;
}

const blankCell = (price: number): Cell => ({ price, stock: 0, compareAt: 0 });

/**
 * Screen 107 — Variant management.
 *
 * An axis is a dimension the product varies along; its options are the values
 * on that dimension. The matrix below is their cartesian product, so turning an
 * option off removes exactly the combinations that used it — no orphan rows.
 *
 * Keys are latin and lowercase because they are what a SKU's combination is
 * built from and what the storefront puts in a query string; labels are the
 * Persian an operator reads. Keeping them separate is what lets a label be
 * renamed without orphaning the SKUs that referenced it.
 */
export function VariantMatrix({
  productId,
  axes: initial,
  skus,
  productSku,
  basePrice,
}: {
  productId: string;
  axes: AdminVariantAxisDto[];
  /** What each combination currently costs and how many are held. */
  skus: AdminSkuDto[];
  /** The product's own code, which each generated SKU code is built from. */
  productSku: string;
  /** The fallback for a combination that has never been priced. */
  basePrice: number;
}) {
  const [axes, setAxes] = useState<AdminVariantAxisDto[]>(initial);

  /*
    Price and stock per combination, keyed by the combination itself rather
    than by SKU id — a combination that has never been sold has no SKU yet, and
    the operator still has to be able to price it. `save` turns this back into
    the SKU list the API replaces.

    Seeded from the SKUs the product already has, so opening this screen shows
    what the shop is actually charging rather than a grid of the base price.
  */
  const [pricing, setPricing] = useState<Record<string, Cell>>(() =>
    Object.fromEntries(
      skus.map((sku) => [
        sku.combination,
        { price: sku.price, stock: sku.stock, compareAt: sku.compareAt ?? 0 },
      ]),
    ),
  );

  const [axisKey, setAxisKey] = useState('');
  const [axisLabel, setAxisLabel] = useState('');
  const [axisKind, setAxisKind] = useState<Kind>('chip');

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const combinations = useMemo(() => {
    // Only available options take part: an option turned off is one the shop
    // is not selling, so it should not produce a row to price.
    const rows = axes.reduce<{ keys: string[]; labels: string[] }[]>(
      (accumulated, axis) =>
        accumulated.flatMap((prefix) =>
          axis.options
            .filter((option) => option.available)
            .map((option) => ({
              keys: [...prefix.keys, option.key],
              labels: [...prefix.labels, option.label],
            })),
        ),
      [{ keys: [], labels: [] }],
    );

    // With no axes the reduce yields one empty row, which is not a combination.
    return axes.length === 0 ? [] : rows;
  }, [axes]);

  function normaliseKey(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .slice(0, 50);
  }

  function addAxis() {
    const key = normaliseKey(axisKey);
    const label = axisLabel.trim();
    if (!key || !label || axes.some((axis) => axis.key === key)) return;

    setAxes((current) => [...current, { key, label, kind: axisKind, options: [] }]);
    setAxisKey('');
    setAxisLabel('');
    setSaved(false);
  }

  function addOption(axisKeyToUpdate: string, option: AdminVariantOptionDto) {
    setSaved(false);
    setAxes((current) =>
      current.map((axis) =>
        axis.key === axisKeyToUpdate ? { ...axis, options: [...axis.options, option] } : axis,
      ),
    );
  }

  function toggleOption(axisKeyToUpdate: string, optionKey: string) {
    setSaved(false);
    setAxes((current) =>
      current.map((axis) =>
        axis.key === axisKeyToUpdate
          ? {
              ...axis,
              options: axis.options.map((option) =>
                option.key === optionKey ? { ...option, available: !option.available } : option,
              ),
            }
          : axis,
      ),
    );
  }

  function removeOption(axisKeyToUpdate: string, optionKey: string) {
    setSaved(false);
    setAxes((current) =>
      current.map((axis) =>
        axis.key === axisKeyToUpdate
          ? { ...axis, options: axis.options.filter((option) => option.key !== optionKey) }
          : axis,
      ),
    );
  }

  /** What one combination costs and how many are held, priced if never set. */
  function cellFor(combination: string): Cell {
    return pricing[combination] ?? { price: basePrice, stock: 0, compareAt: 0 };
  }

  function setCell(combination: string, patch: Partial<Cell>) {
    setSaved(false);
    setPricing((current) => ({
      ...current,
      [combination]: { ...(current[combination] ?? blankCell(basePrice)), ...patch },
    }));
  }

  /*
    Which combinations are priced badly enough that saving would be refused.

    Checked here as well as on the server so the operator sees which row is
    wrong rather than one sentence about a grid of twenty. The two rules are
    the same ones `SaveSkusAsync` enforces: nothing sells at nothing, and a
    «قیمت پیش از تخفیف» below the selling price is not a discount.
  */
  const priceErrors = new Map<string, string>();
  for (const row of combinations) {
    const key = row.keys.join('|');
    const cell = cellFor(key);

    if (cell.price === 0 && cell.compareAt === 0) {
      priceErrors.set(key, 'قیمت این ترکیب را وارد کنید.');
    } else if (cell.compareAt > 0 && cell.compareAt <= cell.price) {
      priceErrors.set(key, 'قیمت پیش از تخفیف باید بیشتر از قیمت فروش باشد.');
    }
  }

  /**
   * The SKU code for a combination that does not have one yet.
   *
   * Built from the product's own code and the option keys, which is what an
   * operator would have typed on screen 108 anyway. Deterministic on purpose:
   * the code is the identity the API matches a SKU by, so the same combination
   * has to produce the same code on every save or each one would insert a
   * duplicate and orphan the row before it.
   */
  function codeFor(keys: string[]): string {
    const stem = (productSku || 'SKU').trim().toUpperCase().replace(/\s+/g, '-');
    return [stem, ...keys.map((key) => key.toUpperCase())].join('-').slice(0, 64);
  }

  /**
   * Saves the axes, then the price and stock of every combination they make.
   *
   * Two requests because they are two resources, and in this order because a
   * SKU names a combination the axes have to already contain. The SKU list is
   * posted whole and replaces what is stored, so codes the product has that are
   * no longer a live combination are dropped — which is what makes turning an
   * option off actually stop selling it.
   *
   * Existing SKUs keep their code, so the API matches them and they keep their
   * ids — see `AdminCatalogueService.SaveSkusAsync`. That is what stops a price
   * edit from emptying the variant out of every basket holding it.
   */
  async function save() {
    if (priceErrors.size > 0) {
      setError('قیمت بعضی ترکیب‌ها کامل نیست؛ ردیف‌های مشخص‌شده را اصلاح کنید.');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await postJson('/api/admin/product-variants', {
        id: productId,
        axes: axes.map((axis) => ({
          key: axis.key,
          label: axis.label,
          kind: axis.kind,
          options: axis.options.map((option) => ({
            key: option.key,
            label: option.label,
            hex: option.hex ?? null,
            available: option.available,
          })),
        })),
      });

      if (combinations.length > 0) {
        const byCombination = new Map(skus.map((sku) => [sku.combination, sku]));

        await postJson('/api/admin/product-skus', {
          id: productId,
          skus: combinations.map((row) => {
            const combination = row.keys.join('|');
            const existing = byCombination.get(combination);
            const cell = cellFor(combination);

            return {
              code: existing?.code ?? codeFor(row.keys),
              barcode: existing?.barcode ?? null,
              combination,
              price: cell.price,
              stock: cell.stock,
              // Zero clears the strike-through; the API stores null for it.
              compareAt: cell.compareAt,
              active: existing?.active ?? true,
            };
          }),
        });
      }

      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره ترکیب‌ها انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-lg p-lg">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="tune" size={22} />
          محورهای تنوع
        </h3>

        {axes.length === 0 ? (
          <p className="text-body-md text-on-surface-variant">
            هنوز محوری تعریف نشده است. برای مثال «رنگ» یا «سایز» را اضافه کنید.
          </p>
        ) : null}

        {axes.map((axis) => (
          <AxisEditor
            key={axis.key}
            axis={axis}
            onAddOption={(option) => addOption(axis.key, option)}
            onToggleOption={(optionKey) => toggleOption(axis.key, optionKey)}
            onRemoveOption={(optionKey) => removeOption(axis.key, optionKey)}
            onRemove={() => {
              setSaved(false);
              setAxes((current) => current.filter((item) => item.key !== axis.key));
            }}
          />
        ))}

        <div className="grid gap-md border-t border-outline-variant pt-lg md:grid-cols-4">
          <Input
            name="axisLabel"
            label="نام محور"
            placeholder="رنگ"
            value={axisLabel}
            onChange={(event) => setAxisLabel(event.target.value)}
          />
          <Input
            name="axisKey"
            label="کلید"
            className="latin"
            placeholder="color"
            hint="انگلیسی — در کد ترکیب SKU ذخیره می‌شود، پس بعداً تغییرش ندهید."
            value={axisKey}
            onChange={(event) => setAxisKey(event.target.value)}
          />
          <Select
            name="axisKind"
            label="نمایش"
            value={axisKind}
            onChange={(event) => setAxisKind(event.target.value as Kind)}
          >
            <option value="chip">متنی</option>
            <option value="swatch">رنگی</option>
          </Select>
          <Button
            type="button"
            icon="add"
            variant="outline"
            onClick={addAxis}
            disabled={!axisLabel.trim() || !normaliseKey(axisKey)}
            className="self-end px-lg"
          >
            افزودن محور
          </Button>
        </div>

        <p className="tabular text-caption text-on-surface-variant">
          {toPersianDigits(combinations.length)} ترکیب ساخته می‌شود.
        </p>
      </Card>

      {/*
        Price and stock sit here, on the screen where the combinations are
        defined, because that is the order the work happens in: an operator
        adding twenty sizes of a brush knows what each one costs as they add it.
        They are stored on the SKU of that combination — the row the checkout
        actually charges from and reserves against — so screen 108 shows the
        same two numbers beside the code and barcode it owns.
      */}
      <DataTable
        rows={combinations.map((row) => ({ id: row.keys.join('|'), labels: row.labels }))}
        rowKey={(row) => row.id}
        emptyTitle="ترکیبی ساخته نشده"
        emptyDescription="برای هر محور حداقل یک گزینه فعال لازم است."
        columns={[
          {
            key: 'combination',
            header: 'ترکیب',
            cell: (row) => row.labels.join(' · '),
          },
          {
            key: 'price',
            header: 'قیمت فروش (تومان)',
            align: 'end',
            cell: (row) => (
              <span className="flex flex-col items-end gap-2xs">
                <input
                  type="text"
                  inputMode="numeric"
                  required
                  aria-label={`قیمت فروش ${row.labels.join(' ')}`}
                  aria-invalid={priceErrors.has(row.id) || undefined}
                  value={toPersianDigits(cellFor(row.id).price)}
                  onChange={(event) => {
                    const next = Number(normalizeDigitsInput(event.target.value));
                    setCell(row.id, { price: Number.isFinite(next) && next >= 0 ? next : 0 });
                  }}
                  title={formatPrice(cellFor(row.id).price)}
                  className={cn(
                    'tabular w-28 rounded border bg-surface-container-lowest px-sm py-1 text-body-md text-on-surface focus:outline-none',
                    priceErrors.has(row.id)
                      ? 'border-error focus:border-error'
                      : 'border-outline-variant focus:border-primary',
                  )}
                />
                {priceErrors.has(row.id) && (
                  <span className="text-caption text-error">{priceErrors.get(row.id)}</span>
                )}
              </span>
            ),
          },
          {
            /*
              This combination's own discount. The operator types what it used
              to cost; the saving is derived rather than stored, so the figure
              on the product page and the figure the checkout charges cannot
              drift apart. Empty means not on sale.
            */
            key: 'compareAt',
            header: 'قیمت پیش از تخفیف',
            align: 'end',
            cell: (row) => {
              const cell = cellFor(row.id);
              const off =
                cell.compareAt > cell.price && cell.compareAt > 0
                  ? Math.round(((cell.compareAt - cell.price) / cell.compareAt) * 100)
                  : 0;

              return (
                <span className="flex flex-col items-end gap-2xs">
                  <input
                    type="text"
                    inputMode="numeric"
                    aria-label={`قیمت پیش از تخفیف ${row.labels.join(' ')}`}
                    placeholder="بدون تخفیف"
                    value={cell.compareAt === 0 ? '' : toPersianDigits(cell.compareAt)}
                    onChange={(event) => {
                      const next = Number(normalizeDigitsInput(event.target.value));
                      setCell(row.id, {
                        compareAt: Number.isFinite(next) && next >= 0 ? next : 0,
                      });
                    }}
                    className="tabular w-28 rounded border border-outline-variant bg-surface-container-lowest px-sm py-1 text-body-md text-on-surface focus:border-primary focus:outline-none"
                  />
                  {off > 0 && (
                    <span className="text-caption text-primary">
                      {toPersianDigits(off)}٪ تخفیف
                    </span>
                  )}
                </span>
              );
            },
          },
          {
            key: 'stock',
            header: 'موجودی',
            align: 'end',
            cell: (row) => (
              <input
                type="text"
                inputMode="numeric"
                aria-label={`موجودی ${row.labels.join(' ')}`}
                value={toPersianDigits(cellFor(row.id).stock)}
                onChange={(event) => {
                  const next = Number(normalizeDigitsInput(event.target.value));
                  setCell(row.id, { stock: Number.isFinite(next) && next >= 0 ? next : 0 });
                }}
                className="tabular w-20 rounded border border-outline-variant bg-surface-container-lowest px-sm py-1 text-body-md text-on-surface focus:border-primary focus:outline-none"
              />
            ),
          },
          {
            key: 'key',
            header: 'کلید',
            secondary: true,
            cell: (row) => <span className="latin text-caption text-on-surface-variant">{row.id}</span>,
          },
        ]}
      />

      <div className="flex flex-wrap items-center gap-md">
        <Button size="lg" loading={saving} onClick={save} className="self-start px-xl">
          ذخیره ترکیب‌ها
        </Button>

        <FormStatus ok={saved ? 'ترکیب‌ها ذخیره شد.' : null} />

        <FormStatus error={error} />
      </div>
    </div>
  );
}

/** One axis and its options — extracted so each keeps its own draft option. */
function AxisEditor({
  axis,
  onAddOption,
  onToggleOption,
  onRemoveOption,
  onRemove,
}: {
  axis: AdminVariantAxisDto;
  onAddOption: (option: AdminVariantOptionDto) => void;
  onToggleOption: (optionKey: string) => void;
  onRemoveOption: (optionKey: string) => void;
  onRemove: () => void;
}) {
  const [label, setLabel] = useState('');
  const [key, setKey] = useState('');
  const [hex, setHex] = useState('#EFE3D0');

  const normalised = key
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 50);

  const duplicate = axis.options.some((option) => option.key === normalised);
  const canAdd = Boolean(label.trim() && normalised && !duplicate);

  function add() {
    if (!canAdd) return;

    onAddOption({
      key: normalised,
      label: label.trim(),
      ...(axis.kind === 'swatch' ? { hex } : null),
      available: true,
    });

    setLabel('');
    setKey('');
  }

  return (
    <fieldset className="flex flex-col gap-sm rounded-lg border border-outline-variant p-md">
      <legend className="flex items-center gap-sm px-xs text-label-md font-medium text-on-surface-variant">
        {axis.label}
        <span className="latin text-caption text-outline">{axis.key}</span>
        <Badge tone="neutral">{axis.kind === 'swatch' ? 'رنگی' : 'متنی'}</Badge>
        <button
          type="button"
          onClick={onRemove}
          aria-label={`حذف محور ${axis.label}`}
          className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
        >
          <Icon name="delete" size={16} />
        </button>
      </legend>

      <div className="flex flex-wrap gap-sm">
        {axis.options.map((option) => (
          <span key={option.key} className="flex items-center">
            <button
              type="button"
              aria-pressed={option.available}
              onClick={() => onToggleOption(option.key)}
              className={cn(
                'flex items-center gap-xs rounded-s-full border px-md py-sm text-label-md font-medium transition-colors',
                option.available
                  ? 'border-primary bg-soft-mint/40 text-primary'
                  : 'border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
              )}
            >
              {option.hex ? (
                <span
                  aria-hidden="true"
                  style={{ backgroundColor: option.hex }}
                  className="h-4 w-4 rounded-full border border-outline-variant"
                />
              ) : null}
              {option.label}
            </button>
            <button
              type="button"
              onClick={() => onRemoveOption(option.key)}
              aria-label={`حذف ${option.label}`}
              className="rounded-e-full border border-s-0 border-outline-variant px-sm py-sm text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
            >
              <Icon name="close" size={16} />
            </button>
          </span>
        ))}

        {axis.options.length === 0 ? (
          <p className="text-caption text-on-surface-variant">
            برای این محور گزینه‌ای تعریف نشده است.
          </p>
        ) : null}
      </div>

      <div className="grid gap-sm md:grid-cols-4">
        <Input
          name={`${axis.key}-label`}
          label="نام گزینه"
          placeholder="کرمی گرم"
          value={label}
          onChange={(event) => setLabel(event.target.value)}
        />
        <Input
          name={`${axis.key}-key`}
          label="کلید"
          className="latin"
          placeholder="cream"
          hint="انگلیسی — بخشی از کد ترکیب SKU است."
          value={key}
          onChange={(event) => setKey(event.target.value)}
          {...(duplicate && normalised ? { error: 'این کلید تکراری است.' } : null)}
        />
        {axis.kind === 'swatch' ? (
          <label className="flex flex-col gap-xs">
            <span className="text-label-md text-on-surface-variant">رنگ</span>
            <input
              type="color"
              value={hex}
              onChange={(event) => setHex(event.target.value)}
              className="h-12 w-full cursor-pointer rounded-lg border border-outline-variant bg-surface-container-lowest px-xs"
            />
          </label>
        ) : null}
        <Button
          type="button"
          icon="add"
          variant="ghost"
          onClick={add}
          disabled={!canAdd}
          className="self-end px-lg"
        >
          افزودن گزینه
        </Button>
      </div>
    </fieldset>
  );
}
