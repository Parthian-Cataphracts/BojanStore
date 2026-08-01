import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getContentEntry } from '@/lib/api/content';

export const metadata: Metadata = { title: 'ویرایش بنر' };

/** Screen 124 — ویرایش بنر. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  // The record id travels to the write endpoint so a save edits this entry
  // rather than creating another one.
  const { id } = await params;
  const entry = await getContentEntry(id);

  if (!entry) notFound();

  return (
    <AdminPage
      title="ویرایش بنر"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'بنرها و اسلایدرها', href: '/content/banners' },
        { label: 'ویرایش بنر' },
      ]}
    >
      <EntityForm
        resource="content"
        entityId={entry.id}
        submitLabel="ذخیره بنر"
        sections={[
        {
          title: 'اطلاعات بنر',
          icon: 'image',
          fields: [
            { name: 'title', label: 'عنوان بنر', value: entry.title, required: true },
            { name: 'cover', label: 'نشانی تصویر', latin: true, value: entry.cover ?? '', required: true },
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
              checked: entry.status !== 'draft',
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
