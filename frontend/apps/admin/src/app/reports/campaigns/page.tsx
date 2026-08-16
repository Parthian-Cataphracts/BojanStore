import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getCampaigns } from '@/lib/api/campaigns';
import { getCampaignPerformance } from '@/lib/api/reports';

export const metadata: Metadata = { title: 'گزارش کمپین‌ها' };

/**
 * Screen 138 - گزارش کمپین‌ها.
 *
 * The counts come from the list endpoint's own `total` and the reach from the
 * performance report, which covers every campaign. They used to be derived from
 * a page of at most 100, so past that the totals simply stopped.
 */
export default async function Page() {
  const [{ items: campaigns, total }, running, performance] = await Promise.all([
    getCampaigns({ pageSize: 100 }),
    // One row asked for, because only the count is wanted.
    getCampaigns({ status: 'running', pageSize: 1 }),
    getCampaignPerformance(),
  ]);

  const reach = performance.reduce((sum, entry) => sum + entry.reach, 0);
  const bestConversion = performance.length > 0
    ? Math.max(...performance.map((entry) => entry.conversion))
    : 0;

  const kpis = [
    { label: 'کل کمپین‌ها', value: toPersianDigits(total), icon: 'campaign' },
    { label: 'در حال اجرا', value: toPersianDigits(running.total), icon: 'play_circle' },
    { label: 'مجموع دسترسی', value: toPersianDigits(reach), icon: 'visibility' },
    {
      label: 'بهترین نرخ تبدیل',
      value: `${toPersianDigits(bestConversion)}٪`,
      icon: 'trending_up',
    },
  ];

  const columns = [
    { key: 'title', header: 'کمپین', cell: (row: (typeof campaigns)[number]) => row.title },
    { key: 'reach', header: 'دسترسی', cell: (row: (typeof campaigns)[number]) => <span className="tabular">{toPersianDigits(row.reach)}</span> },
    { key: 'conv', header: 'نرخ تبدیل', cell: (row: (typeof campaigns)[number]) => <span className="tabular">{toPersianDigits(row.conversion)}٪</span> },
  ];

  const rows = campaigns;

  return (
    <AdminPage
      title="گزارش کمپین‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'گزارش‌ها' }, { label: 'کمپین‌ها' }]}
      actions={
        <Link href="/reports/export" className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}>
          <Icon name="download" size={18} />
          خروجی گرفتن
        </Link>
      }
    >
      <ReportRangePicker />
      <KpiRow items={kpis} />

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="داده‌ای برای این بازه نیست"
        emptyIcon="bar_chart"
      />
    </AdminPage>
  );
}
