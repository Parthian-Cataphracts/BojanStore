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

const initial: Attribute[] = [
  { id: 'a-1', name: 'جنس کاغذ', kind: 'متن', values: 'بالکی، تحریر، گلاسه', filterable: true },
  { id: 'a-2', name: 'گرماژ', kind: 'عدد', values: '۷۰، ۸۰، ۹۰، ۱۰۰', filterable: true },
  { id: 'a-3', name: 'نوع صحافی', kind: 'متن', values: 'دوخت، فنر، چسب گرم', filterable: false },
  { id: 'a-4', name: 'تعداد برگ', kind: 'عدد', values: '۸۰، ۱۰۰، ۱۲۰، ۱۶۰', filterable: false },
];

/** Screen 106 — Product attributes. */
export function AttributeTable() {
  const [rows, setRows] = useState(initial);

  return (
    <div className="flex flex-col gap-lg">
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

        <Button icon="add" className="self-start px-lg">
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
