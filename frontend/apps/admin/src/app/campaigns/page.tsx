import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Card, Icon, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
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
      {/*
        Creating a campaign tells nobody about it, and nothing on this screen
        used to say so. A campaign is a period with a discount or a banner
        attached; announcing it is a separate, deliberate act with its own
        screen — reasonably, since one is reversible and the other lands in
        everybody's pocket. What was missing was any sign from here that the
        second step exists, so an owner set up a campaign, saw nothing happen,
        and concluded the feature did not work.
      */}
      <Card className="flex flex-wrap items-center justify-between gap-md p-md">
        <span className="flex items-start gap-sm text-caption leading-relaxed text-on-surface-variant">
          <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
          ساخت کمپین به مشتری‌ها اطلاع نمی‌دهد. برای خبر دادن، از «ارسال اعلان» استفاده کنید —
          اعلان درون‌برنامه‌ای، پیامک یا نوتیفیکیشن مرورگر.
        </span>
        <Link
          href="/campaigns/notifications"
          className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
        >
          <Icon name="send" size={18} />
          ارسال اعلان
        </Link>
      </Card>

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
        /*
          The edit screen has existed at /campaigns/[id] all along and nothing
          linked to it. This table had no actions column at all, so the only way
          to open a campaign was to know its id and type the address — which
          means that from the panel, a campaign could be created and then never
          touched again. The same fault the settings screens had before they
          were put in the sidebar.
        */
        actions={(row) => (
          <Link
            href={`/campaigns/${row.id}`}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
