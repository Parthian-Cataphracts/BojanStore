import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';

export const metadata: Metadata = { title: 'افزودن پرسش' };

/** Screen 126 — افزودن پرسش. */
export default function Page() {
  return (
    <AdminPage
      title="افزودن پرسش"
      breadcrumbs={[
        { label: 'محتوا', href: '/content' },
        { label: 'سوالات متداول', href: '/content/faq' },
        { label: 'افزودن پرسش' },
      ]}
    >
      <EntityForm
        resource="content"
        fixedFields={{ kind: 'faq' }}
        submitLabel="ذخیره پرسش"
        sections={[
        {
          title: 'پرسش و پاسخ',
          icon: 'help',
          fields: [
            { name: 'title', label: 'متن پرسش', required: true },
            { name: 'body', label: 'متن پاسخ', kind: 'textarea' },
            { name: 'excerpt', label: 'دسته‌بندی', hint: 'گروهی که این پرسش زیر آن نمایش داده می‌شود — مثلاً «ارسال» یا «پرداخت». چیپ‌های صفحه‌ی سوالات متداول از روی همین ساخته می‌شوند.' },
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
