import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { VerificationSettingsForm } from '@/components/VerificationSettingsForm';
import { getVerificationSettings } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'تایید ایمیل و شماره' };

/**
 * تایید ایمیل و شماره — whether a customer's email and phone must be proven
 * before the account can use them.
 *
 * Its own screen rather than a section of «تنظیمات فروشگاه»: like پیامک and
 * اعلان مرورگر beside it, this configures a capability the storefront checks
 * against, not a preference about copy.
 */
export default async function Page() {
  const settings = await getVerificationSettings();

  return (
    <AdminPage
      title="تایید ایمیل و شماره"
      description="الزام تایید ایمیل و شماره موبایل برای مشتریان."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'تایید ایمیل و شماره' },
      ]}
    >
      <VerificationSettingsForm settings={settings} />
    </AdminPage>
  );
}
