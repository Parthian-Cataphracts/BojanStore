import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'مدیریت اتصال‌ها و یکپارچه‌سازی‌ها' };

/** Screen 154 - مدیریت اتصال‌ها و یکپارچه‌سازی‌ها. */
export default async function Page() {
  await requireRole('owner');
  const settings = await getSettingsSection('integrations');

  return (
    <AdminPage
      title="مدیریت اتصال‌ها و یکپارچه‌سازی‌ها"
      description="سرویس‌های خارجی متصل به فروشگاه."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'اتصال‌ها' }]}
    >
      <SettingsForm section="integrations"
        sections={[
          { title: 'پیامک', icon: 'sms', fields: withSavedValues([
            { name: 'smsProvider', label: 'سرویس‌دهنده', kind: 'select', options: ['کاوه‌نگار', 'قاصدک', 'ملی‌پیامک'] },
            { name: 'smsKey', label: 'کلید API', kind: 'text', value: 'sk_test_********', latin: true },
          ], settings) },
          { title: 'تحلیل', icon: 'analytics', fields: withSavedValues([
            { name: 'analyticsId', label: 'شناسه Google Analytics', kind: 'text', value: 'G-XXXXXXXXXX', latin: true },
            { name: 'analyticsEnabled', label: 'ارسال داده فعال باشد', kind: 'switch', checked: true },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
