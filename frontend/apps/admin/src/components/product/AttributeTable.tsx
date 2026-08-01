'use client';

import { useState } from 'react';
import { Button, Card, Icon, Input, Select } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';

interface Attribute {
  id: string;
  name: string;
  kind: string;
  values: string;
  filterable: boolean;
}

/**
 * Screen 106 — Product attributes.
 *
 * No endpoint defines attributes: `resources.ts` has no entry, and nothing
 * under the catalogue writes them. This screen previously listed four invented
 * rows — paper stock, weight, binding, sheet count — identically for every
 * product, and its add button did nothing, so an operator could believe those
 * were the open product's attributes and that removing one had removed it.
 *
 * The form stays laid out, because the shape is right for the day this is
 * wired, but nothing here claims to hold or save anything until it is.
 */
export function AttributeTable() {
  const [rows, setRows] = useState<Attribute[]>([]);

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex items-start gap-sm border-primary/30 p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          تعریف ویژگی‌های محصول هنوز در سرور پیاده‌سازی نشده است. این بخش پس از افزوده‌شدن آن فعال
          می‌شود.
        </p>
      </Card>

      <Card className="flex flex-col gap-md p-lg">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="add_circle" size={22} />
          افزودن ویژگی
        </h3>

        <div className="grid gap-md md:grid-cols-4">
          <Input name="name" label="نام ویژگی" placeholder="مثال: جنس کاغذ" />
          <Select name="kind" label="نوع مقدار" defaultValue="text">
            <option value="text">متن</option>
            <option value="number">عدد</option>
            <option value="boolean">بله / خیر</option>
          </Select>
          <Input
            name="values"
            label="مقادیر مجاز"
            placeholder="با کاما جدا کنید"
            wrapperClassName="md:col-span-2"
          />
        </div>

        <Button
          icon="add"
          disabled
          title="تعریف ویژگی هنوز در سرور پیاده‌سازی نشده است."
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
          { key: 'kind', header: 'نوع', cell: (row) => row.kind },
          { key: 'values', header: 'مقادیر', cell: (row) => row.values },
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
            onClick={() => setRows((current) => current.filter((item) => item.id !== row.id))}
            className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
          >
            <Icon name="delete" size={18} />
          </button>
        )}
      />
    </div>
  );
}
