'use client';

import { useState } from 'react';
import { Badge, Button, Card, Code, Icon, Input, formatPrice, toPersianDigits } from '@bojan/ui';
import { DataTable } from '@/components/DataTable';

interface Sku {
  id: string;
  code: string;
  barcode: string;
  variant: string;
  stock: number;
  price: number;
  active: boolean;
}

const initial: Sku[] = [
  { id: 's-1', code: 'BZ-PLN-A5-CRM', barcode: '6260100234561', variant: 'کرمی · A5', stock: 24, price: 350_000, active: true },
  { id: 's-2', code: 'BZ-PLN-A4-CRM', barcode: '6260100234578', variant: 'کرمی · A4', stock: 8, price: 420_000, active: true },
  { id: 's-3', code: 'BZ-PLN-A5-TEA', barcode: '6260100234585', variant: 'سبزآبی · A5', stock: 0, price: 350_000, active: false },
];

/** Screen 108 — SKU management. */
export function SkuTable() {
  const [rows, setRows] = useState(initial);

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-md p-lg">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="qr_code_2" size={22} />
          افزودن SKU
        </h3>

        <div className="grid gap-md md:grid-cols-3">
          <Input name="code" label="کد SKU" className="latin" placeholder="BZ-PLN-A5-CRM" />
          <Input name="barcode" label="بارکد" className="latin" inputMode="numeric" />
          <Input name="variant" label="ترکیب" placeholder="کرمی · A5" />
        </div>

        <Button icon="add" className="self-start px-lg">
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
          { key: 'barcode', header: 'بارکد', cell: (row) => <Code>{row.barcode}</Code> },
          { key: 'variant', header: 'ترکیب', cell: (row) => row.variant },
          {
            key: 'stock',
            header: 'موجودی',
            cell: (row) => <span className="tabular">{toPersianDigits(row.stock)}</span>,
          },
          { key: 'price', header: 'قیمت', cell: (row) => formatPrice(row.price) },
          {
            key: 'active',
            header: 'وضعیت',
            cell: (row) =>
              row.active ? <Badge tone="mint">فعال</Badge> : <Badge tone="neutral">غیرفعال</Badge>,
          },
        ]}
        actions={(row) => (
          <button
            type="button"
            aria-label={`حذف ${row.code}`}
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
