import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات فروشگاه' };

/** Screen 141 - تنظیمات فروشگاه. */
export default async function Page() {
  const settings = await getSettingsSection('store');

  return (
    <AdminPage
      title="تنظیمات فروشگاه"
      description="نام، اطلاعات تماس و تنظیمات پایه فروشگاه."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'فروشگاه' }]}
    >
      <SettingsForm section="store"
        sections={[
          { title: 'اطلاعات فروشگاه', icon: 'storefront', fields: withSavedValues([
            { name: 'storeName', label: 'نام فروشگاه', kind: 'text', value: 'بوژان' },
            { name: 'tagline', label: 'شعار', kind: 'text', value: 'برای لحظه‌های خلاق زندگی' },
            { name: 'email', label: 'ایمیل پشتیبانی', kind: 'text', value: 'info@bojan.com', latin: true },
            { name: 'phone', label: 'تلفن پشتیبانی', kind: 'text', value: '۰۲۱-۱۲۳۴۵۶۷۸' },
          ], settings) },
          { title: 'آدرس', icon: 'place', fields: withSavedValues([
            { name: 'address', label: 'آدرس فروشگاه', kind: 'textarea', value: 'تهران، خیابان ولیعصر، کوچه فرزان، پلاک ۱۲' },
            { name: 'postalCode', label: 'کد پستی', kind: 'text', value: '۱۹۶۸۸۴۳۵۶۱' },
          ], settings) },
          /*
            Only the switch that switches something.

            «خرید بدون ثبت‌نام» sat here reading on by default and the checkout
            has always required a signed-in customer — every route under `/me`
            derives the customer from a credential, and an order has a customer
            id it cannot do without. Guest checkout is a feature, not a setting,
            and a switch claiming it was already on told the owner their shop
            did something it does not do.
          */
          { title: 'عمومی', icon: 'tune', fields: withSavedValues([
            { name: 'maintenance', label: 'حالت تعمیر و نگهداری', kind: 'switch' },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
