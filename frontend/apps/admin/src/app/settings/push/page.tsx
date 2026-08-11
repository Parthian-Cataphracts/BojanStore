import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { WebPushSettingsForm } from '@/components/WebPushSettingsForm';
import { getWebPushSettings } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'اعلان مرورگر' };

/**
 * اعلان مرورگر — the shop's Web Push identity.
 *
 * Beside the SMS and gateway screens rather than under the notification
 * composer: what it configures is the shop's ability to send on that channel at
 * all, not a preference about what to send.
 */
export default async function Page() {
  const settings = await getWebPushSettings();

  return (
    <AdminPage
      title="اعلان مرورگر"
      description="کلید سرور و روشن بودن کانال اعلان مرورگر."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'اعلان مرورگر' },
      ]}
    >
      <WebPushSettingsForm settings={settings} />
    </AdminPage>
  );
}
