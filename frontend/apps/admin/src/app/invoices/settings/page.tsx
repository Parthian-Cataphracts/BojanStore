import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { InvoiceSettingsForm } from '@/components/InvoiceSettingsForm';
import { getInvoiceSettings } from '@/lib/api/invoice-settings';

export const metadata: Metadata = { title: 'تنظیمات فاکتور' };

/**
 * The parts of the invoice that are the shop's words rather than an order's
 * facts — seller identity, closing text, and the electronic stamp.
 *
 * Reached from the invoices screen rather than from the settings group, because
 * this is where someone stands when they notice the document says the wrong
 * thing. The API gates every write here on the owner role and the settings
 * section, so a sales or support operator following the link is refused there
 * as well as here.
 */
export default async function InvoiceSettingsPage() {
  const settings = await getInvoiceSettings();

  return (
    <AdminPage
      title="تنظیمات فاکتور"
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'فاکتورها', href: '/invoices' },
        { label: 'تنظیمات فاکتور' },
      ]}
    >
      <p className="text-body-sm leading-relaxed text-on-surface-variant">
        این اطلاعات روی همه‌ی فاکتورها چاپ می‌شود — هم نسخه‌ای که در پنل باز می‌کنید و هم نسخه‌ای که
        مشتری از حساب کاربری‌اش دانلود می‌کند. تغییرات بلافاصله روی فاکتورهای صادرشده‌ی قبلی هم اعمال
        می‌شود، چون سند در هر بار نمایش دوباره ساخته می‌شود.
      </p>

      <InvoiceSettingsForm settings={settings} />
    </AdminPage>
  );
}
