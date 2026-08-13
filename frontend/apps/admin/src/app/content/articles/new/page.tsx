import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';

export const metadata: Metadata = { title: 'افزودن مقاله' };

/** Screen 123 — افزودن مقاله. */
export default function Page() {
  return (
    <AdminPage
      title="افزودن مقاله"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'مقالات مجله', href: '/content/articles' },
        { label: 'افزودن مقاله' },
      ]}
    >
      <EntityForm
        resource="content"
        fixedFields={{ kind: 'article' }}
        submitLabel="ذخیره مقاله"
        sections={[
        {
          title: 'محتوای مقاله',
          icon: 'article',
          fields: [
            { name: 'title', label: 'عنوان مقاله', required: true },
            { name: 'slug', label: 'نشانی (slug)', latin: true },
            { name: 'excerpt', label: 'خلاصه', kind: 'textarea' },
            { name: 'body', label: 'متن مقاله', kind: 'textarea' },
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
        {
          title: 'تصویر کاور',
          icon: 'image',
          fields: [
            {
              name: 'cover',
              label: 'تصویر کاور',
              kind: 'image',
              folder: 'content',
              hint: 'تصویر بارگذاری می‌شود؛ نشانی از جای دیگر پذیرفته نمی‌شود.',
            },
          ],
        },
      ]}
      />
    </AdminPage>
  );
}
