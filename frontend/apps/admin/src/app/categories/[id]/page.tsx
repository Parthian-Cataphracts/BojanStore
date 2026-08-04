import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getCategories, getCategory } from '@/lib/api/categories';

export const metadata: Metadata = { title: 'ویرایش دسته‌بندی' };

/**
 * Screen 100 — ویرایش دسته‌بندی.
 *
 * The record is looked up from the route parameter and its values seed the
 * form. Without that the screen rendered an empty form whichever category was
 * opened, and saving would have created a new one instead of editing this one.
 */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const category = await getCategory(id);

  if (!category) notFound();

  const { items: allCategories } = await getCategories({ pageSize: 200 });
  const parentOptions = allCategories
    .filter((item) => item.id !== category.id)
    .map((item) => ({ value: item.id, label: item.name }));

  return (
    <AdminPage
      title="ویرایش دسته‌بندی"
      breadcrumbs={[{ label: 'دسته‌بندی‌ها', href: '/categories' }, { label: category.name }]}
    >
      <EntityForm
        resource="categories"
        entityId={category.id}
        submitLabel="ذخیره دسته‌بندی"
        sections={[
          {
            title: 'اطلاعات دسته‌بندی',
            icon: 'category',
            fields: [
              { name: 'title', label: 'نام دسته‌بندی', value: category.name, required: true },
              {
                name: 'slug',
                label: 'نشانی (slug)',
                value: category.slug,
                latin: true,
                hint: 'در URL استفاده می‌شود.',
              },
              {
                name: 'parentId',
                label: 'دسته‌بندی والد',
                kind: 'select',
                ...(category.parentId ? { value: category.parentId } : null),
                options: parentOptions,
              },
              // No "توضیحات" field here: `Category` has no description column
              // to save it into or read it back from — the API accepts the
              // key and silently drops it. A field that cannot be read back
              // is worse than none; the operator would type something and
              // never learn it was never kept.
            ],
          },
          {
            title: 'سئو',
            icon: 'travel_explore',
            fields: [
              { name: 'metaTitle', label: 'عنوان متا', value: category.metaTitle ?? '' },
              {
                name: 'metaDescription',
                label: 'توضیحات متا',
                kind: 'textarea',
                value: category.metaDescription ?? '',
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
                checked: category.status !== 'draft',
                onValue: 'published',
                offValue: 'draft',
              },
              {
                name: 'showInMenu',
                label: 'نمایش در منوی اصلی',
                kind: 'switch',
                checked: category.showInMenu ?? true,
              },
              { name: 'order', label: 'ترتیب نمایش', kind: 'number', value: String(category.order ?? 0) },
            ],
          },
          {
            title: 'تصویر و آیکون',
            icon: 'image',
            fields: [
              {
                name: 'icon',
                label: 'نام آیکون',
                value: category.icon,
                latin: true,
                hint: 'از Material Symbols، مثلاً palette',
              },
            ],
          },
        ]}
      />
    </AdminPage>
  );
}
