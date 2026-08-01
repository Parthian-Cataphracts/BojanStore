import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { VariantMatrix } from '@/components/product/VariantMatrix';

export const metadata: Metadata = { title: 'مدیریت تنوع محصول' };

/** Screen 107 — مدیریت تنوع محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  return (
    <AdminPage
      title="مدیریت تنوع محصول"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: 'ویرایش محصول', href: `/products/${id}` },
        { label: 'مدیریت تنوع محصول' },
      ]}
    >
      <VariantMatrix />
    </AdminPage>
  );
}
