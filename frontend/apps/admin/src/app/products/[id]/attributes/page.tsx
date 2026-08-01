import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { AttributeTable } from '@/components/product/AttributeTable';
import { getProduct, getProductAttributes } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت ویژگی‌های محصول' };

/** Screen 106 — مدیریت ویژگی‌های محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const [product, attributes] = await Promise.all([getProduct(id), getProductAttributes(id)]);
  if (!product) notFound();

  return (
    <AdminPage
      title="مدیریت ویژگی‌های محصول"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: product.title, href: `/products/${id}` },
        { label: 'مدیریت ویژگی‌های محصول' },
      ]}
    >
      <AttributeTable productId={id} attributes={attributes} />
    </AdminPage>
  );
}
