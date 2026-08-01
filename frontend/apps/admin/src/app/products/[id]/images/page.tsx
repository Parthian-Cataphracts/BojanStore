import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { ProductImagesPanel } from '@/components/product/ProductImagesPanel';

export const metadata: Metadata = { title: 'مدیریت تصاویر محصول' };

/** Screen 105 — مدیریت تصاویر محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  return (
    <AdminPage
      title="مدیریت تصاویر محصول"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: 'ویرایش محصول', href: `/products/${id}` },
        { label: 'مدیریت تصاویر محصول' },
      ]}
    >
      <ProductImagesPanel />
    </AdminPage>
  );
}
