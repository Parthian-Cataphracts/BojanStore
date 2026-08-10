'use client';

import { useState } from 'react';
import { Button, Card, Checkbox, FormStatus, Icon, Input, Select } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';
import { postJson } from '@/lib/submit';
import type { AdminAttributeDto } from '@/lib/api/types';

type Kind = AdminAttributeDto['kind'];

const KIND_LABELS: Record<Kind, string> = {
  text: 'متن',
  number: 'عدد',
  boolean: 'بله / خیر',
};

/** Row identity is local: an unsaved row has no server id yet. */
interface Row extends Omit<AdminAttributeDto, 'id'> {
  id: string;
}

/**
 * Screen 106 — Product attributes.
 *
 * The whole list is posted on save and the API replaces what it holds, so a
 * removal here is a removal there. Nothing is written until save, which is why
 * adding a row only touches local state.
 */
export function AttributeTable({
  productId,
  attributes,
}: {
  productId: string;
  attributes: AdminAttributeDto[];
}) {
  const [rows, setRows] = useState<Row[]>(attributes);
  const [name, setName] = useState('');
  const [kind, setKind] = useState<Kind>('text');
  const [values, setValues] = useState('');
  const [filterable, setFilterable] = useState(false);

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const trimmedName = name.trim();
  const duplicate = rows.some((row) => row.name === trimmedName);

  function add() {
    if (!trimmedName || duplicate) return;

    setRows((current) => [
      ...current,
      {
        // Local only — the API assigns the real id on save.
        id: `new-${current.length}-${trimmedName}`,
        name: trimmedName,
        kind,
        values: values
          .split(/[,،]/)
          .map((value) => value.trim())
          .filter(Boolean),
        filterable,
      },
    ]);

    setName('');
    setValues('');
    setFilterable(false);
    setSaved(false);
  }

  async function save() {
    setSaving(true);
    setError(null);
    try {
      await postJson('/api/admin/product-attributes', {
        id: productId,
        attributes: rows.map((row) => ({
          name: row.name,
          kind: row.kind,
          values: row.values,
          filterable: row.filterable,
        })),
      });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره ویژگی‌ها انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-md p-lg">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="add_circle" size={22} />
          افزودن ویژگی
        </h3>

        <div className="grid gap-md md:grid-cols-4">
          <Input
            name="name"
            label="نام ویژگی"
            placeholder="مثال: جنس کاغذ"
            value={name}
            onChange={(event) => setName(event.target.value)}
            {...(duplicate && trimmedName ? { error: 'این ویژگی قبلاً اضافه شده است.' } : null)}
          />
          <Select
            name="kind"
            label="نوع مقدار"
            value={kind}
            onChange={(event) => setKind(event.target.value as Kind)}
          >
            <option value="text">متن</option>
            <option value="number">عدد</option>
            <option value="boolean">بله / خیر</option>
          </Select>
          <Input
            name="values"
            label="مقادیر مجاز"
            placeholder="با کاما جدا کنید"
            wrapperClassName="md:col-span-2"
            value={values}
            onChange={(event) => setValues(event.target.value)}
          />
        </div>

        <Checkbox
          name="filterable"
          label="این ویژگی در فیلترهای فروشگاه نمایش داده شود."
          checked={filterable}
          onChange={(event) => setFilterable(event.target.checked)}
        />

        <Button
          icon="add"
          type="button"
          onClick={add}
          disabled={!trimmedName || duplicate}
          className="self-start px-lg"
        >
          افزودن به فهرست
        </Button>
      </Card>

      <DataTable
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="ویژگی‌ای تعریف نشده"
        emptyDescription="برای فیلتر کردن محصولات، حداقل یک ویژگی اضافه کنید."
        columns={[
          { key: 'name', header: 'نام ویژگی', cell: (row) => row.name },
          { key: 'kind', header: 'نوع', cell: (row) => KIND_LABELS[row.kind] },
          {
            key: 'values',
            header: 'مقادیر',
            cell: (row) => (row.values.length > 0 ? row.values.join('، ') : '—'),
          },
          {
            key: 'filterable',
            header: 'قابل فیلتر',
            cell: (row) => (row.filterable ? 'بله' : 'خیر'),
          },
        ]}
        actions={(row) => (
          <button
            type="button"
            aria-label={`حذف ${row.name}`}
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
          ذخیره ویژگی‌ها
        </Button>

        <FormStatus ok={saved ? 'ویژگی‌ها ذخیره شد.' : null} />

        <FormStatus error={error} />
      </div>
    </div>
  );
}
