import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { ReportExporter } from '@/components/ReportExporter';

export const metadata: Metadata = { title: 'خروجی گرفتن از گزارش‌ها' };

/** Screen 140 — Export reports. */
export default function ReportExportPage() {
  return (
    <AdminPage
      title="خروجی گرفتن از گزارش‌ها"
      description="گزارش مورد نظر، بازه زمانی و قالب خروجی را انتخاب کنید."
      breadcrumbs={[{ label: 'گزارش‌ها', href: '/reports/sales' }, { label: 'خروجی گرفتن' }]}
    >
      <ReportExporter />
    </AdminPage>
  );
}
