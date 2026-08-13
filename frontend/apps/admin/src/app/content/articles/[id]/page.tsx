import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getContentEntry } from '@/lib/api/content';

export const metadata: Metadata = { title: 'ویرایش مقاله' };

/** Screen 123 — ویرایش مقاله. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  // The record id travels to the write endpoint so a save edits this entry
  // rather than creating another one.
  const { id } = await params;
  const entry = await getContentEntry(id);

  if (!entry) notFound();

  return (
    <AdminPage
      title="ویرایش مقاله"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'مقالات مجله', href: '/content/articles' },
        { label: 'ویرایش مقاله' },
      ]}
    >
      <EntityForm
        resource="content"
        entityId={entry.id}
        submitLabel="ذخیره مقاله"
        archive={{ noun: 'مقاله', returnTo: '/content/articles' }}
        sections={[
        {
          title: 'محتوای مقاله',
          icon: 'article',
          fields: [
            { name: 'title', label: 'عنوان مقاله', value: entry.title, required: true },
            { name: 'slug', label: 'نشانی (slug)', latin: true, value: entry.slug ?? '' },
            { name: 'excerpt', label: 'خلاصه', kind: 'textarea', value: entry.excerpt ?? '' },
            { name: 'body', label: 'متن مقاله', kind: 'textarea', value: entry.body ?? '' },
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
              value: entry.status,
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
              value: entry.cover ?? '',
              hint: 'تصویر بارگذاری می‌شود؛ نشانی از جای دیگر پذیرفته نمی‌شود.',
            },
          ],
        },
      ]}
      />
    </AdminPage>
  );
}
