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

/**
 * Screen 108 — SKU management.
 *
 * A product carries one `Sku` on its own record and there is no per-variant SKU
 * anywhere in the domain, so nothing can store what this screen edits. It used
 * to show three invented codes with their own barcodes, stock and prices,
 * identically for every product — numbers an operator could act on believing
 * they were real.
 */
export function SkuTable() {
  const [rows, setRows] = useState<Sku[]>([]);

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex items-start gap-sm border-primary/30 p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          مدیریت SKU به ازای هر ترکیب هنوز در سرور پیاده‌سازی نشده است. کد SKU اصلی محصول را از صفحه
          ویرایش محصول تغییر دهید.
        </p>
      </Card>

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

        <Button
          icon="add"
          disabled
          title="مدیریت SKU هنوز در سرور پیاده‌سازی نشده است."
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
