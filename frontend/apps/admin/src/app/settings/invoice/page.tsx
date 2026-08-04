import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات مالیات' };

/**
 * Screen 144 — tax.
 *
 * It used to be "تنظیمات فاکتور و مالیات" and carried three fields describing
 * things the product does not do:
 *
 * - `invoicePrefix` promised invoice numbers beginning `INV-`. They are sixteen
 *   random digits with no prefix, issued at delivery. That `INV-` was left over
 *   from the placeholder screen 34 drew before invoices were real.
 * - `legalName` and `economicCode` were collected here and printed nowhere.
 *   They are the seller block on *فاکتورها ← تنظیمات فاکتور* now, which is the
 *   screen that actually puts them on the document.
 *
 * What remains is tax, and the note at the foot says plainly that it is
 * recorded rather than applied — the same rule the rest of this panel follows
 * about not showing a control that quietly does nothing.
 */
export default async function Page() {
  const settings = await getSettingsSection('invoice');

  return (
    <AdminPage
      title="تنظیمات مالیات"
      description="نرخ مالیات بر ارزش افزوده."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'مالیات' },
      ]}
      actions={
        <Link
          href="/invoices/settings"
          className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
        >
          <Icon name="receipt_long" size={18} />
          تنظیمات فاکتور
        </Link>
      }
    >
      <p className="text-body-sm leading-relaxed text-on-surface-variant">
        مشخصات فروشنده، متن‌های فاکتور و مهر فروشگاه در صفحه‌ی{' '}
        <Link href="/invoices/settings" className="text-primary underline">
          تنظیمات فاکتور
        </Link>{' '}
        تنظیم می‌شوند — همان‌جا که روی سند چاپ می‌شوند.
      </p>

      <SettingsForm
        section="invoice"
        sections={[
          {
            title: 'مالیات',
            icon: 'receipt_long',
            fields: withSavedValues(
              [
                {
                  name: 'vatRate',
                  label: 'نرخ مالیات بر ارزش افزوده',
                  kind: 'text',
                  value: '۹',
                  suffix: 'درصد',
                },
                {
                  name: 'vatEnabled',
                  label: 'محاسبه مالیات فعال باشد',
                  kind: 'switch',
                  checked: true,
                },
              ],
              settings,
            ),
          },
        ]}
      />

      <p className="text-caption leading-relaxed text-on-surface-variant">
        این نرخ فعلاً فقط ثبت می‌شود و روی مبلغ سفارش‌ها اعمال نمی‌شود؛ سفارش‌ها ستون مالیات ندارند.
        اعمال واقعی آن نیازمند افزوده‌شدن مالیات به سفارش، محاسبه در تسویه‌حساب و یک سطر روی فاکتور
        است.
      </p>
    </AdminPage>
  );
}
