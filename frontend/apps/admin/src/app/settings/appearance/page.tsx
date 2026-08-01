import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات ظاهر و ناوبری' };

/** Screen 150 - تنظیمات ظاهر و ناوبری. */
export default async function Page() {
  const settings = await getSettingsSection('appearance');

  return (
    <AdminPage
      title="تنظیمات ظاهر و ناوبری"
      description="منوی اصلی، بنرها و رنگ‌بندی فروشگاه."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'ظاهر' }]}
    >
      <SettingsForm section="appearance"
        sections={[
          { title: 'برند', icon: 'palette', fields: withSavedValues([
            { name: 'brandName', label: 'نام نمایشی برند', kind: 'text', value: 'بوژان' },
            { name: 'primaryColor', label: 'رنگ اصلی', kind: 'text', value: '#003441', latin: true },
            { name: 'accentColor', label: 'رنگ تأکید', kind: 'text', value: '#F36F5D', latin: true },
          ], settings) },
          { title: 'ناوبری', icon: 'menu', fields: withSavedValues([
            { name: 'showMagazine', label: 'نمایش مجله در منو', kind: 'switch', checked: true },
            { name: 'showB2B', label: 'نمایش خرید سازمانی در منو', kind: 'switch', checked: true },
            { name: 'showLoyalty', label: 'نمایش باشگاه مشتریان', kind: 'switch', checked: true },
          ], settings) },
        ]}
      />
    </AdminPage>
  );
}
