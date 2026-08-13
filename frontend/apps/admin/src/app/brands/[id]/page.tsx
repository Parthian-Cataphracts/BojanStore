import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getBrand } from '@/lib/api/brands';

export const metadata: Metadata = { title: 'ویرایش برند' };

/**
 * Screen 102 — ویرایش برند.
 *
 * Looks the brand up from the route parameter so the form opens on the record
 * being edited rather than on empty fields.
 */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const brand = await getBrand(id);

  if (!brand) notFound();

  return (
    <AdminPage
      title="ویرایش برند"
      breadcrumbs={[{ label: 'برندها', href: '/brands' }, { label: brand.name }]}
    >
      <EntityForm
        resource="brands"
        entityId={brand.id}
        submitLabel="ذخیره برند"
        sections={[
          {
            title: 'اطلاعات برند',
            icon: 'branding_watermark',
            fields: [
              { name: 'title', label: 'نام برند', value: brand.name, required: true },
              { name: 'slug', label: 'نشانی (slug)', value: brand.slug, latin: true },
              { name: 'tagline', label: 'شعار / توضیح کوتاه', value: brand.tagline ?? '' },
              { name: 'description', label: 'معرفی برند', kind: 'textarea', value: brand.description ?? '' },
              { name: 'country', label: 'کشور سازنده', value: brand.country ?? '' },
            ],
          },
          {
            title: 'سئو',
            icon: 'travel_explore',
            fields: [
              { name: 'metaTitle', label: 'عنوان متا', value: brand.metaTitle ?? '' },
              {
                name: 'metaDescription',
                label: 'توضیحات متا',
                kind: 'textarea',
                value: brand.metaDescription ?? '',
              },
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
                label: 'فعال باشد',
                kind: 'switch',
                checked: brand.status !== 'draft',
                onValue: 'published',
                offValue: 'draft',
              },
              { name: 'featured', label: 'برند ویژه', kind: 'switch', checked: brand.featured },
            ],
          },
          {
            title: 'لوگو',
            icon: 'image',
            fields: [
              {
                name: 'logo',
                label: 'لوگوی برند',
                kind: 'image',
                folder: 'brands',
                value: brand.logo ?? '',
                hint: 'تصویر بارگذاری می‌شود؛ نشانی از جای دیگر پذیرفته نمی‌شود.',
              },
            ],
          },
        ]}
      />
    </AdminPage>
  );
}
