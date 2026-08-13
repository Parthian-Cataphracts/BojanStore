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
            { name: 'excerpt', label: 'زیرعنوان', hint: 'جمله‌ی زیر عنوان روی همان بنر' },
            { name: 'slug', label: 'جایگاه (slug)', latin: true,
              hint: 'برای بنر بالای صفحه‌ی اصلی، دقیقاً home-hero' },
            {
              name: 'cover',
              label: 'تصویر بنر',
              kind: 'image',
              folder: 'content',
              hint: 'تصویر بارگذاری می‌شود؛ نشانی از جای دیگر پذیرفته نمی‌شود.',
            },
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
