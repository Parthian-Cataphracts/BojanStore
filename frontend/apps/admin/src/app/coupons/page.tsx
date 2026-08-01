import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Code, Icon, buttonClasses, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getCoupons } from '@/lib/api/coupons';
import type { AdminCouponDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت کدهای تخفیف' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<AdminCouponDto>[] = [
  { key: 'code', header: 'کد', cell: (row) => <Code className="font-semibold text-primary">{row.code}</Code> },
  { key: 'title', header: 'عنوان', cell: (row) => row.title },
  { key: 'value', header: 'مقدار', cell: (row) => <span className="tabular">{row.percent ? `${toPersianDigits(row.percent)}٪` : formatPrice(row.amount ?? 0)}</span> },
  { key: 'usage', header: 'استفاده', cell: (row) => <span className="tabular">{toPersianDigits(row.usedCount)} از {toPersianDigits(row.usageLimit)}</span> },
  { key: 'expires', header: 'انقضا', cell: (row) => <span className="tabular">{row.expiresAt ? formatDate(row.expiresAt) : '—'}</span> },
  { key: 'active', header: 'فعال', cell: (row) => row.active ? <Badge tone="mint">فعال</Badge> : <Badge tone="neutral">غیرفعال</Badge> },
];

/** Screen 128 - مدیریت کدهای تخفیف. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();

  const { items: rows } = await getCoupons({ q: query, pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت کدهای تخفیف"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'کدهای تخفیف' }]}
      actions={
        <Link href="/coupons/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          کد جدید
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی کد تخفیف..." />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="کد تخفیفی یافت نشد"
        emptyIcon="local_offer"
        actions={(row) => (
          <Link href={`/coupons/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
