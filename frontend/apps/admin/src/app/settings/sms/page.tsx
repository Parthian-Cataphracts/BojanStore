import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SmsSettingsForm } from '@/components/SmsSettingsForm';
import { getSmsSettings } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'تنظیمات پیامک' };

/**
 * تنظیمات پیامک — the SMS account the shop signs customers in with.
 *
 * Its own screen rather than a section of the notifications one, because what
 * it configures is not a preference: without it the sign-in code never leaves
 * the building and no customer can reach their account.
 */
export default async function Page() {
  const settings = await getSmsSettings();

  return (
    <AdminPage
      title="تنظیمات پیامک"
      description="حساب سرویس پیامک، قالب کد ورود و خط ارسال کمپین‌ها."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'پیامک' },
      ]}
    >
      <SmsSettingsForm settings={settings} />
    </AdminPage>
  );
}
