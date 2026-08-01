import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';

export const metadata: Metadata = { title: 'افزودن بنر' };

/** Screen 124 — افزودن بنر. */
export default function Page() {
  return (
    <AdminPage
      title="افزودن بنر"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'بنرها و اسلایدرها', href: '/content/banners' },
        { label: 'افزودن بنر' },
      ]}
    >
      <EntityForm
        resource="content"
        fixedFields={{ kind: 'banner' }}
        submitLabel="ذخیره بنر"
        sections={[
        {
          title: 'اطلاعات بنر',
          icon: 'image',
          fields: [
            { name: 'title', label: 'عنوان بنر', required: true },
            { name: 'cover', label: 'نشانی تصویر', latin: true, required: true },
          ],
        },
      ]}
        asideSections={[
        {
          title: 'نمایش',
          icon: 'visibility',
          fields: [
            {
              name: 'status',
              label: 'فعال باشد',
              kind: 'switch',
              checked: true,
              onValue: 'published',
              offValue: 'draft',
            },
          ],
        },
      ]}
      />
    </AdminPage>
  );
}
