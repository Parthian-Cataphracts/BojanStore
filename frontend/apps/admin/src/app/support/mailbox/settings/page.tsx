import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { MailboxSettingsForm } from '@/components/MailboxSettingsForm';
import { getMailboxSettings } from '@/lib/api/mailbox';

export const metadata: Metadata = { title: 'تنظیمات صندوق پستی' };

/**
 * Where the support mailbox is pointed.
 *
 * Owner-only on the API, and narrower than the support role that reads the
 * inbox: these settings carry the credential to a mail account, and someone
 * trusted to answer customers is not thereby trusted to point the shop's
 * support address at a server of their choosing.
 */
export default async function MailboxSettingsPage() {
  const settings = await getMailboxSettings();

  return (
    <AdminPage
      title="تنظیمات صندوق پستی"
      description="اتصال به حساب ایمیلی که مشتریان به آن پیام می‌فرستند."
      breadcrumbs={[
        { label: 'پشتیبانی', href: '/support' },
        { label: 'صندوق پستی', href: '/support/mailbox' },
        { label: 'تنظیمات' },
      ]}
    >
      <p className="text-body-sm leading-relaxed text-on-surface-variant">
        این حساب جدا از سرویس ارسال ایمیل‌های تراکنشی (سفارش و بازیابی گذرواژه) است. عمداً جداست:
        اشتباه در تنظیمات این صفحه فقط صندوق پشتیبانی را از کار می‌اندازد و به رسیدن ایمیل سفارش‌ها
        به مشتری دست نمی‌زند.
      </p>

      <MailboxSettingsForm settings={settings} />
    </AdminPage>
  );
}
