import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { VariantMatrix } from '@/components/product/VariantMatrix';
import { getProduct, getProductVariants } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت تنوع محصول' };

/** Screen 107 — مدیریت تنوع محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const [product, axes] = await Promise.all([getProduct(id), getProductVariants(id)]);
  if (!product) notFound();

  return (
    <AdminPage
      title="مدیریت تنوع محصول"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: product.title, href: `/products/${id}` },
        { label: 'مدیریت تنوع محصول' },
      ]}
    >
      <VariantMatrix productId={id} axes={axes} />
    </AdminPage>
  );
}
