import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { PaymentSettingsForm } from '@/components/PaymentSettingsForm';
import { getPaymentSettings } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'تنظیمات پرداخت' };

/**
 * Screen 143 — تنظیمات پرداخت.
 *
 * This screen used to write into the generic settings table, where nothing read
 * it: an owner could choose a gateway, type a merchant id, switch cash on
 * delivery off, and none of it changed what the shop did. It now reads and
 * writes the real gateway configuration and the shop's own payment-method rows.
 */
export default async function Page() {
  const settings = await getPaymentSettings();

  return (
    <AdminPage
      title="تنظیمات پرداخت"
      description="درگاه بانکی و روش‌های پرداختی که در تسویه‌حساب به مشتری نشان داده می‌شوند."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'پرداخت' },
      ]}
    >
      <PaymentSettingsForm settings={settings} />
    </AdminPage>
  );
}
