import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات اعلان‌های ادمین' };

/** Screen 148 - تنظیمات اعلان‌های ادمین. */
export default async function Page() {
  const settings = await getSettingsSection('notifications');

  return (
    <AdminPage
      title="تنظیمات اعلان‌های ادمین"
      description="کدام رویدادها به کدام کانال اطلاع داده شوند."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'اعلان‌ها' }]}
    >
      <SettingsForm section="notifications"
        sections={[
          { title: 'کانال‌ها', icon: 'notifications', fields: withSavedValues([
            { name: 'email', label: 'اعلان ایمیلی', kind: 'switch', checked: true },
            { name: 'sms', label: 'اعلان پیامکی', kind: 'switch', checked: true },
            { name: 'inApp', label: 'اعلان داخل پنل', kind: 'switch', checked: true },
          ], settings) },
          { title: 'رویدادها', icon: 'event', fields: withSavedValues([
            { name: 'newOrder', label: 'سفارش جدید', kind: 'switch', checked: true },
            { name: 'lowStock', label: 'کمبود موجودی', kind: 'switch', checked: true },
            { name: 'newTicket', label: 'تیکت پشتیبانی جدید', kind: 'switch', checked: true },
            { name: 'b2bRequest', label: 'درخواست سازمانی جدید', kind: 'switch', checked: true },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
