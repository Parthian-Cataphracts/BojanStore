import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { ReportExporter } from '@/components/ReportExporter';
import { requireAdminSession } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'خروجی گرفتن از گزارش‌ها' };

/** Screen 140 — Export reports. */
export default async function ReportExportPage() {
  // The financial report is owner-only on the API, so the picker is built from
  // who is asking rather than offering a choice that would be refused.
  const session = await requireAdminSession();

  return (
    <AdminPage
      title="خروجی گرفتن از گزارش‌ها"
      description="گزارش مورد نظر، بازه زمانی و قالب خروجی را انتخاب کنید."
      breadcrumbs={[{ label: 'گزارش‌ها', href: '/reports/sales' }, { label: 'خروجی گرفتن' }]}
    >
      <ReportExporter role={session.role} />
    </AdminPage>
  );
}
