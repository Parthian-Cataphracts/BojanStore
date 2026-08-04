import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getCategories } from '@/lib/api/categories';

export const metadata: Metadata = { title: 'افزودن دسته‌بندی' };

/** Screen 100 — افزودن دسته‌بندی. */
export default async function Page() {
  const { items: allCategories } = await getCategories({ pageSize: 200 });
  const parentOptions = allCategories.map((item) => ({ value: item.id, label: item.name }));

  return (
    <AdminPage
      title="افزودن دسته‌بندی"
      breadcrumbs={[{ label: 'دسته‌بندی‌ها', href: '/categories' }, { label: 'افزودن دسته‌بندی' }]}
    >
      <EntityForm
        resource="categories"
        submitLabel="ذخیره دسته‌بندی"
        sections={[
        {
          title: 'اطلاعات دسته‌بندی',
          icon: 'category',
          fields: [
            { name: 'title', label: 'نام دسته‌بندی', required: true },
            { name: 'slug', label: 'نشانی (slug)', latin: true, hint: 'در URL استفاده می‌شود.' },
            { name: 'parentId', label: 'دسته‌بندی والد', kind: 'select', options: parentOptions },
            // No "توضیحات" field: `Category` has no description column for
            // it to be saved into.
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
            { name: 'showInMenu', label: 'نمایش در منوی اصلی', kind: 'switch', checked: true },
            { name: 'order', label: 'ترتیب نمایش', kind: 'number' },
          ],
        },
        {
          title: 'تصویر و آیکون',
          icon: 'image',
          fields: [
            { name: 'icon', label: 'نام آیکون', latin: true, hint: 'از Material Symbols، مثلاً palette' },
          ],
        },
      ]}
      />
    </AdminPage>
  );
}
