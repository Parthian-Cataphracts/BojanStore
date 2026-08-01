import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';

export const metadata: Metadata = { title: 'افزودن صفحه' };

/** Screen 125 — افزودن صفحه. */
export default function Page() {
  return (
    <AdminPage
      title="افزودن صفحه"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'صفحات ثابت', href: '/content/pages' },
        { label: 'افزودن صفحه' },
      ]}
    >
      <EntityForm
        resource="content"
        fixedFields={{ kind: 'page' }}
        submitLabel="ذخیره صفحه"
        sections={[
        {
          title: 'محتوای صفحه',
          icon: 'description',
          fields: [
            { name: 'title', label: 'عنوان صفحه', required: true },
            { name: 'slug', label: 'نشانی (slug)', latin: true },
            { name: 'body', label: 'متن صفحه', kind: 'textarea' },
          ],
        },
      ]}
        asideSections={[
        {
          title: 'انتشار',
          icon: 'visibility',
          fields: [
            {
              name: 'status',
              label: 'وضعیت',
              kind: 'select',
              options: [
                { value: 'draft', label: 'پیش‌نویس' },
                { value: 'published', label: 'منتشر شده' },
              ],
            },
          ],
        },
      ]}
      />
    </AdminPage>
  );
}
