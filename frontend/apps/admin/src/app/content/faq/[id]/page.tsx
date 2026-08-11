import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getContentEntry } from '@/lib/api/content';

export const metadata: Metadata = { title: 'ویرایش پرسش' };

/** Screen 126 — ویرایش پرسش. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  // The record id travels to the write endpoint so a save edits this entry
  // rather than creating another one.
  const { id } = await params;
  const entry = await getContentEntry(id);

  if (!entry) notFound();

  return (
    <AdminPage
      title="ویرایش پرسش"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'سوالات متداول', href: '/content/faq' },
        { label: 'ویرایش پرسش' },
      ]}
    >
      <EntityForm
        resource="content"
        entityId={entry.id}
        submitLabel="ذخیره پرسش"
        sections={[
        {
          title: 'پرسش و پاسخ',
          icon: 'help',
          fields: [
            { name: 'title', label: 'متن پرسش', value: entry.title, required: true },
            { name: 'body', label: 'متن پاسخ', kind: 'textarea', value: entry.body ?? '' },
            { name: 'excerpt', label: 'دسته‌بندی', value: entry.excerpt ?? '', hint: 'گروهی که این پرسش زیر آن نمایش داده می‌شود — مثلاً «ارسال» یا «پرداخت». چیپ‌های صفحه‌ی سوالات متداول از روی همین ساخته می‌شوند.' },
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
