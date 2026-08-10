'use client';

import { useMemo, useState } from 'react';
import { Badge, Button, Card, FormStatus, Icon, Input, Select, cn, toPersianDigits } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { postJson } from '@/lib/submit';
import type { AdminVariantAxisDto, AdminVariantOptionDto } from '@/lib/api/types';

type Kind = AdminVariantAxisDto['kind'];

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
}: {
  productId: string;
  axes: AdminVariantAxisDto[];
}) {
  const [axes, setAxes] = useState<AdminVariantAxisDto[]>(initial);

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

  async function save() {
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
            key: 'key',
            header: 'کلید',
            cell: (row) => <span className="latin text-caption text-on-surface-variant">{row.id}</span>,
          },
        ]}
      />

      {/* Price and stock per combination live on screen 108, where each
          combination is a SKU with its own code and barcode. Repeating those
          columns here would be two places to edit one number. */}
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
