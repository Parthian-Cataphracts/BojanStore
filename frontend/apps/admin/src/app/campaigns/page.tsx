import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Icon, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getCampaigns } from '@/lib/api/campaigns';
import { campaignStatusMeta } from '@/lib/status';
import type { CampaignDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت کمپین‌ها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<CampaignDto>[] = [
  { key: 'title', header: 'عنوان', cell: (row) => row.title },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => {
      const status = row.status as keyof typeof campaignStatusMeta;
      return <Badge tone={campaignStatusMeta[status].tone}>{campaignStatusMeta[status].label}</Badge>;
    },
  },
  {
    key: 'period',
    header: 'بازه',
    cell: (row) => (
      <span className="tabular">
        {row.startsAt ? formatDate(row.startsAt) : '—'} تا {row.endsAt ? formatDate(row.endsAt) : '—'}
      </span>
    ),
  },
  { key: 'reach', header: 'دسترسی', cell: (row) => <span className="tabular">{toPersianDigits(row.reach)}</span> },
  { key: 'conv', header: 'نرخ تبدیل', cell: (row) => <span className="tabular">{toPersianDigits(row.conversion)}٪</span> },
];

/** Screen 127 - مدیریت کمپین‌ها. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const status = first(params.status);

  const { items: rows } = await getCampaigns({ q: query, status, pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت کمپین‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'کمپین‌ها' }]}
      actions={
        <Link href="/campaigns/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          کمپین جدید
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی کمپین..."
          filters={[
            {
              param: 'status',
              label: 'وضعیت',
              options: [
                { value: 'running', label: 'در حال اجرا' },
                { value: 'scheduled', label: 'زمان‌بندی شده' },
                { value: 'ended', label: 'پایان یافته' },
              ],
            },
          ]} />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="کمپینی یافت نشد"
        emptyIcon="campaign"

      />
    </AdminPage>
  );
}
