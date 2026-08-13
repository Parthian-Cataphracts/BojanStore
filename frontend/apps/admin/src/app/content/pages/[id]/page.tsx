import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getContentEntry } from '@/lib/api/content';

export const metadata: Metadata = { title: 'ویرایش صفحه' };

/** Screen 125 — ویرایش صفحه. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  // The record id travels to the write endpoint so a save edits this entry
  // rather than creating another one.
  const { id } = await params;
  const entry = await getContentEntry(id);

  if (!entry) notFound();

  return (
    <AdminPage
      title="ویرایش صفحه"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'صفحات ثابت', href: '/content/pages' },
        { label: 'ویرایش صفحه' },
      ]}
    >
      <EntityForm
        resource="content"
        entityId={entry.id}
        submitLabel="ذخیره صفحه"
        archive={{ noun: 'صفحه', returnTo: '/content/pages' }}
        sections={[
        {
          title: 'محتوای صفحه',
          icon: 'description',
          fields: [
            { name: 'title', label: 'عنوان صفحه', value: entry.title, required: true },
            { name: 'slug', label: 'نشانی (slug)', latin: true, value: entry.slug ?? '' },
            { name: 'body', label: 'متن صفحه', kind: 'textarea', value: entry.body ?? '' },
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
      ]}
      />
    </AdminPage>
  );
}
