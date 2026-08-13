import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';

export const metadata: Metadata = { title: 'افزودن کالکشن' };


/** Screen 104 — افزودن کالکشن. */
export default function Page() {
  return (
    <AdminPage
      title="افزودن کالکشن"
      breadcrumbs={[{ label: 'کالکشن‌ها', href: '/collections' }, { label: 'افزودن کالکشن' }]}
    >
      <EntityForm
        resource="collections"
        submitLabel="ذخیره کالکشن"
        sections={[
        {
          title: 'اطلاعات کالکشن',
          icon: 'collections_bookmark',
          fields: [
            { name: 'title', label: 'عنوان کالکشن', required: true },
            { name: 'slug', label: 'نشانی (slug)', latin: true },
            { name: 'summary', label: 'توضیح کوتاه', kind: 'textarea' },
            { name: 'editorialNote', label: 'یادداشت سردبیر', kind: 'textarea' },
          ],
        },
      ]}
        asideSections={[
        {
          title: 'انتشار',
          icon: 'visibility',
          fields: [
            { name: 'status', label: 'فعال باشد', kind: 'switch', checked: true, onValue: 'published', offValue: 'draft' },
            { name: 'featured', label: 'کالکشن ویژه صفحه اصلی', kind: 'switch' },
          ],
        },
        {
          title: 'تصویر کاور',
          icon: 'image',
          fields: [
            {
              name: 'cover',
              label: 'تصویر کالکشن',
              kind: 'image',
              folder: 'collections',
              hint: 'تصویر بارگذاری می‌شود؛ نشانی از جای دیگر پذیرفته نمی‌شود.',
            },
          ],
        },
      ]}
      />
    </AdminPage>
  );
}
