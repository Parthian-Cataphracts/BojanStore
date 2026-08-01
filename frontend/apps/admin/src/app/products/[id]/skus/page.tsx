import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { SkuTable } from '@/components/product/SkuTable';
import { getProduct, getProductSkus, getProductVariants } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت SKU' };

/** Screen 108 — مدیریت SKU. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  // The axes come along so the table can offer the combinations that exist and
  // render each SKU's stored keys as the labels an operator recognises.
  const [product, skus, axes] = await Promise.all([
    getProduct(id),
    getProductSkus(id),
    getProductVariants(id),
  ]);

  if (!product) notFound();

  return (
    <AdminPage
      title="مدیریت SKU"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: product.title, href: `/products/${id}` },
        { label: 'مدیریت SKU' },
      ]}
    >
      <SkuTable productId={id} skus={skus} axes={axes} defaultPrice={product.price} />
    </AdminPage>
  );
}
