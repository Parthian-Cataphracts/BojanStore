import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getCampaigns } from '@/lib/api/campaigns';

export const metadata: Metadata = { title: 'گزارش کمپین‌ها' };

/** Screen 138 - گزارش کمپین‌ها. */
export default async function Page() {
  const { items: campaigns } = await getCampaigns({ pageSize: 100 });
  const reach = campaigns.reduce((sum, c) => sum + c.reach, 0);
  const running = campaigns.filter((c) => c.status === 'running');

  const kpis = [
    { label: 'کل کمپین‌ها', value: toPersianDigits(campaigns.length), icon: 'campaign' },
    { label: 'در حال اجرا', value: toPersianDigits(running.length), icon: 'play_circle' },
    { label: 'مجموع دسترسی', value: toPersianDigits(reach), icon: 'visibility' },
    {
      label: 'بهترین نرخ تبدیل',
      value: `${toPersianDigits(campaigns.length ? Math.max(...campaigns.map((c) => c.conversion)) : 0)}٪`,
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
        emptyIcon="analytics"
      />
    </AdminPage>
  );
}
