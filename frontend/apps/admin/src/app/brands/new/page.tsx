import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';

export const metadata: Metadata = { title: 'افزودن برند' };


/** Screen 102 — افزودن برند. */
export default function Page() {
  return (
    <AdminPage
      title="افزودن برند"
      breadcrumbs={[{ label: 'برندها', href: '/brands' }, { label: 'افزودن برند' }]}
    >
      <EntityForm
        resource="brands"
        submitLabel="ذخیره برند"
        sections={[
        {
          title: 'اطلاعات برند',
          icon: 'branding_watermark',
          fields: [
            { name: 'title', label: 'نام برند', required: true },
            { name: 'slug', label: 'نشانی (slug)', latin: true },
            { name: 'tagline', label: 'شعار / توضیح کوتاه' },
            { name: 'description', label: 'معرفی برند', kind: 'textarea' },
            { name: 'country', label: 'کشور سازنده' },
          ],
        },
        {
          title: 'سئو',
          icon: 'travel_explore',
          fields: [
            { name: 'metaTitle', label: 'عنوان متا' },
            { name: 'metaDescription', label: 'توضیحات متا', kind: 'textarea' },
          ],
        },
      ]}
        asideSections={[
        {
          title: 'انتشار',
          icon: 'visibility',
          fields: [
            { name: 'status', label: 'فعال باشد', kind: 'switch', checked: true, onValue: 'published', offValue: 'draft' },
            { name: 'featured', label: 'برند ویژه', kind: 'switch' },
          ],
        },
        {
          title: 'لوگو',
          icon: 'image',
          fields: [{ name: 'logo', label: 'نشانی لوگو', latin: true }],
        },
      ]}
      />
    </AdminPage>
  );
}
