import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات فاکتور و مالیات' };

/** Screen 144 - تنظیمات فاکتور و مالیات. */
export default async function Page() {
  const settings = await getSettingsSection('invoice');

  return (
    <AdminPage
      title="تنظیمات فاکتور و مالیات"
      description="اطلاعات مالیاتی و قالب فاکتور رسمی."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'فاکتور' }]}
    >
      <SettingsForm section="invoice"
        sections={[
          { title: 'مالیات', icon: 'receipt_long', fields: withSavedValues([
            { name: 'vatRate', label: 'نرخ مالیات بر ارزش افزوده', kind: 'text', value: '۹', suffix: 'درصد' },
            { name: 'vatEnabled', label: 'محاسبه مالیات فعال باشد', kind: 'switch', checked: true },
          ], settings) },
          { title: 'اطلاعات فاکتور', icon: 'description', fields: withSavedValues([
            { name: 'legalName', label: 'نام حقوقی', kind: 'text', value: 'بوژان' },
            { name: 'economicCode', label: 'کد اقتصادی', kind: 'text', value: '۴۱۱۲۳۴۵۶۷۸۹۰' },
            { name: 'invoicePrefix', label: 'پیشوند شماره فاکتور', kind: 'text', value: 'INV-', latin: true },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
