import type { Metadata } from 'next';
import { Badge, Card, Icon, formatDateTime, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { mockServices } from '@/lib/mock';
import { healthMeta } from '@/lib/status';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'وضعیت سیستم و سلامت سرویس‌ها' };

/** Screen 157 — Service health. */
export default async function SystemHealthPage() {
  await requireRole('owner');
  const degraded = mockServices.filter((service) => service.status !== 'operational');
  const averageLatency = Math.round(
    mockServices.reduce((sum, service) => sum + service.latencyMs, 0) / mockServices.length,
  );

  return (
    <AdminPage
      title="وضعیت سیستم و سلامت سرویس‌ها"
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'وضعیت سیستم' },
      ]}
    >
      <KpiRow
        items={[
          { label: 'سرویس‌های پایش‌شده', value: toPersianDigits(mockServices.length), icon: 'monitor_heart' },
          {
            label: 'سرویس‌های سالم',
            value: toPersianDigits(mockServices.length - degraded.length),
            icon: 'check_circle',
          },
          {
            label: 'نیازمند بررسی',
            value: toPersianDigits(degraded.length),
            icon: 'warning',
            ...(degraded.length > 0 ? { delta: 'بررسی کنید', up: false } : null),
          },
          { label: 'میانگین تأخیر', value: `${toPersianDigits(averageLatency)} ms`, icon: 'speed' },
        ]}
      />

      {degraded.length > 0 && (
        <Card className="flex items-start gap-sm border-secondary-container/50 bg-secondary-fixed/40 p-md">
          <Icon name="warning" size={20} className="mt-px shrink-0 text-secondary" />
          <p className="text-body-md leading-relaxed text-on-secondary-fixed-variant">
            {degraded.map((service) => service.name).join('، ')} در وضعیت عادی نیست.
          </p>
        </Card>
      )}

      <DataTable
        columns={[
          { key: 'name', header: 'سرویس', cell: (row) => row.name },
          {
            key: 'status',
            header: 'وضعیت',
            cell: (row) => {
              const meta = healthMeta[row.status];
              return (
                <Badge tone={meta.tone}>
                  <Icon name={meta.icon} size={16} />
                  {meta.label}
                </Badge>
              );
            },
          },
          {
            key: 'latency',
            header: 'تأخیر',
            cell: (row) => <span className="tabular">{toPersianDigits(row.latencyMs)} ms</span>,
          },
          {
            key: 'checked',
            header: 'آخرین بررسی',
            cell: (row) => <span className="tabular">{formatDateTime(row.checkedAt)}</span>,
          },
        ]}
        rows={mockServices}
        rowKey={(row) => row.id}
        emptyTitle="سرویسی پایش نمی‌شود"
        emptyIcon="monitor_heart"
      />
    </AdminPage>
  );
}
