import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات پرداخت' };

/** Screen 143 - تنظیمات پرداخت. */
export default async function Page() {
  const settings = await getSettingsSection('payment');

  return (
    <AdminPage
      title="تنظیمات پرداخت"
      description="درگاه‌های بانکی و روش‌های پرداخت فعال."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'پرداخت' }]}
    >
      <SettingsForm section="payment"
        sections={[
          { title: 'درگاه بانکی', icon: 'credit_card', fields: withSavedValues([
            { name: 'gateway', label: 'درگاه فعال', kind: 'select', options: ['سامان', 'ملت', 'زرین‌پال'] },
            { name: 'merchantId', label: 'شناسه پذیرنده', kind: 'text', value: 'sk_live_8f2a', latin: true },
          ], settings) },
          { title: 'روش‌های پرداخت', icon: 'payments', fields: withSavedValues([
            { name: 'online', label: 'پرداخت اینترنتی', kind: 'switch', checked: true },
            { name: 'wallet', label: 'کیف پول بوژان', kind: 'switch', checked: true },
            { name: 'cod', label: 'پرداخت در محل', kind: 'switch' },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
