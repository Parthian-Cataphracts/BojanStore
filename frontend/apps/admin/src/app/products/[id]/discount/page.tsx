import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { DiscountPanel } from '@/components/product/DiscountPanel';
import { getProduct, getProductSkus } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت تخفیف محصول' };

/** Screen 110 — مدیریت تخفیف محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  // Whether combinations price this product decides whether a discount set
  // here reaches a shopper at all — see the notice in DiscountPanel. Caught
  // rather than fatal: a failed read must not take down the discount screen,
  // and saying nothing is the same as the screen behaved before.
  const [product, skus] = await Promise.all([getProduct(id), getProductSkus(id).catch(() => [])]);

  if (!product) notFound();

  return (
    <AdminPage
      title="مدیریت تخفیف محصول"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: 'ویرایش محصول', href: `/products/${id}` },
        { label: 'مدیریت تخفیف محصول' },
      ]}
    >
      <DiscountPanel product={product} variantPriced={skus.some((sku) => sku.active)} />
    </AdminPage>
  );
}
