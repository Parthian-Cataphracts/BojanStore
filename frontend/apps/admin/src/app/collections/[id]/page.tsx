import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { CollectionProductsPanel } from '@/components/CollectionProductsPanel';
import { EntityForm } from '@/components/EntityForm';
import { getProductOptions } from '@/lib/api/catalogue';
import { getCollection } from '@/lib/api/collections';

export const metadata: Metadata = { title: 'ویرایش کالکشن' };

/**
 * Screen 104 — ویرایش کالکشن.
 *
 * Looks the collection up from the route parameter so the form opens on the
 * record being edited rather than on empty fields.
 */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const [collection, products] = await Promise.all([getCollection(id), getProductOptions()]);

  if (!collection) notFound();

  return (
    <AdminPage
      title="ویرایش کالکشن"
      breadcrumbs={[{ label: 'کالکشن‌ها', href: '/collections' }, { label: collection.title }]}
    >
      <EntityForm
        resource="collections"
        entityId={collection.id}
        submitLabel="ذخیره کالکشن"
        sections={[
          {
            title: 'اطلاعات کالکشن',
            icon: 'collections_bookmark',
            fields: [
              { name: 'title', label: 'عنوان کالکشن', value: collection.title, required: true },
              { name: 'slug', label: 'نشانی (slug)', value: collection.slug, latin: true },
              { name: 'summary', label: 'توضیح کوتاه', kind: 'textarea', value: collection.summary ?? '' },
              { name: 'editorialNote', label: 'یادداشت سردبیر', kind: 'textarea', value: collection.editorialNote ?? '' },
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
                checked: collection.status !== 'draft',
                onValue: 'published',
                offValue: 'draft',
              },
              {
                name: 'featured',
                label: 'کالکشن ویژه صفحه اصلی',
                kind: 'switch',
                checked: collection.featured,
              },
            ],
          },
          {
            title: 'تصویر کاور',
            icon: 'image',
            fields: [
              {
                name: 'cover',
                label: 'تصویر کالکشن',
                kind: 'image',
                folder: 'collections',
                value: collection.cover ?? '',
                hint: 'تصویر بارگذاری می‌شود؛ نشانی از جای دیگر پذیرفته نمی‌شود.',
              },
            ],
          },
        ]}
      />

      {/*
        Outside the entity form and saved on its own button: the membership is
        posted whole and replaces what is stored, so carrying it along with the
        title would rewrite the collection's contents on every save of a field
        that has nothing to do with them.
      */}
      <CollectionProductsPanel
        collectionId={collection.id}
        products={products}
        selected={collection.productSlugs ?? []}
      />
    </AdminPage>
  );
}
