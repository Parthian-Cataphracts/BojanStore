import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات ارسال و تحویل' };

/** Screen 142 - تنظیمات ارسال و تحویل. */
export default async function Page() {
  const settings = await getSettingsSection('shipping');

  return (
    <AdminPage
      title="تنظیمات ارسال و تحویل"
      description="روش‌های ارسال، هزینه‌ها و آستانه ارسال رایگان."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'ارسال' }]}
    >
      <SettingsForm section="shipping"
        sections={[
          { title: 'روش‌های ارسال', icon: 'local_shipping', fields: withSavedValues([
            { name: 'standardPrice', label: 'هزینه ارسال استاندارد', kind: 'text', value: '۴۵,۰۰۰', suffix: 'تومان' },
            { name: 'expressPrice', label: 'هزینه ارسال سریع', kind: 'text', value: '۸۵,۰۰۰', suffix: 'تومان' },
            { name: 'courierPrice', label: 'هزینه پیک ویژه', kind: 'text', value: '۱۲۰,۰۰۰', suffix: 'تومان' },
          ], settings) },
          { title: 'ارسال رایگان', icon: 'redeem', fields: withSavedValues([
            { name: 'freeThreshold', label: 'حداقل مبلغ ارسال رایگان', kind: 'text', value: '۱,۰۰۰,۰۰۰', suffix: 'تومان' },
            { name: 'freeEnabled', label: 'ارسال رایگان فعال باشد', kind: 'switch', checked: true },
          ], settings) },
          { title: 'زمان‌بندی', icon: 'schedule', fields: withSavedValues([
            { name: 'cutoff', label: 'ساعت برش سفارش روزانه', kind: 'text', value: '۱۲:۰۰' },
            { name: 'sameDay', label: 'تحویل همان روز (تهران)', kind: 'switch', checked: true },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
