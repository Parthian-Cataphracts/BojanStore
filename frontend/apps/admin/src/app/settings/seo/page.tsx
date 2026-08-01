import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات سئو و متادیتا' };

/** Screen 149 - تنظیمات سئو و متادیتا. */
export default async function Page() {
  const settings = await getSettingsSection('seo');

  return (
    <AdminPage
      title="تنظیمات سئو و متادیتا"
      description="عنوان، توضیحات و نقشه سایت."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'سئو' }]}
    >
      <SettingsForm section="seo"
        sections={[
          { title: 'متادیتای پیش‌فرض', icon: 'travel_explore', fields: withSavedValues([
            { name: 'metaTitle', label: 'عنوان پیش‌فرض', kind: 'text', value: 'بوژان - برای لحظه‌های خلاق زندگی' },
            { name: 'metaDescription', label: 'توضیحات پیش‌فرض', kind: 'textarea', value: 'از نوشت‌افزار و دفتر تا ابزار هنری، معماری، هدیه و اکسسوری‌های خاص.' },
            { name: 'canonical', label: 'دامنه اصلی', kind: 'text', value: 'https://bojan.com', latin: true },
          ], settings) },
          { title: 'نقشه سایت و ربات‌ها', icon: 'robot', fields: withSavedValues([
            { name: 'sitemap', label: 'تولید خودکار sitemap.xml', kind: 'switch', checked: true },
            { name: 'indexing', label: 'اجازه ایندکس شدن', kind: 'switch', checked: true },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
