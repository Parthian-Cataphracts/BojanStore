import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { ShippingMethodsForm } from '@/components/ShippingMethodsForm';
import { getShippingMethods } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'تنظیمات ارسال و تحویل' };

/**
 * Screen 142 — تنظیمات ارسال و تحویل.
 *
 * It used to write three prices into the generic settings table, where nothing
 * read them: the shop charged whatever the seeder had written, and there was no
 * way at all to change a shipping price without a deploy. The fields are now
 * the shop's own shipping tiers.
 */
export default async function Page() {
  const methods = await getShippingMethods();

  return (
    <AdminPage
      title="تنظیمات ارسال و تحویل"
      description="روش‌های ارسال، هزینه و زمان تحویلی که در صفحه‌ی تسویه‌حساب به مشتری نشان داده می‌شوند."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'ارسال' },
      ]}
    >
      <ShippingMethodsForm methods={methods} />
    </AdminPage>
  );
}
