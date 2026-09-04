import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { VariantMatrix } from '@/components/product/VariantMatrix';
import { getProduct, getProductSkus, getProductVariants } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت تنوع محصول' };

/** Screen 107 — مدیریت تنوع محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  // The SKUs come along because each combination's price and stock live on
  // one, and this screen is where they are now set — a shopper who picks a
  // size pays that size's price, so the operator has to be able to type it
  // where they define the size.
  const [product, axes, skus] = await Promise.all([
    getProduct(id),
    getProductVariants(id),
    getProductSkus(id),
  ]);
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
      <VariantMatrix
        productId={id}
        axes={axes}
        skus={skus}
        productSku={product.sku}
        basePrice={product.price}
      />
    </AdminPage>
  );
}
