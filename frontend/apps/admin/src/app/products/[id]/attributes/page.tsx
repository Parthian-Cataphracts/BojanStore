import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { AttributeTable } from '@/components/product/AttributeTable';

export const metadata: Metadata = { title: 'مدیریت ویژگی‌های محصول' };

/** Screen 106 — مدیریت ویژگی‌های محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  return (
    <AdminPage
      title="مدیریت ویژگی‌های محصول"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: 'ویرایش محصول', href: `/products/${id}` },
        { label: 'مدیریت ویژگی‌های محصول' },
      ]}
    >
      <AttributeTable />
    </AdminPage>
  );
}
