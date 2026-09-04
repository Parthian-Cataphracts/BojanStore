import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { PricingPanel } from '@/components/product/PricingPanel';
import { getProduct, getProductSkus } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت قیمت‌ها' };

/** Screen 109 — مدیریت قیمت‌ها. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  // Combinations decide whether there is anything for this screen to edit —
  // see PricingPanel. Caught rather than fatal: a failed read must not take the
  // screen down, and an empty list is how it behaved before.
  const [product, skus] = await Promise.all([getProduct(id), getProductSkus(id).catch(() => [])]);

  if (!product) notFound();

  return (
    <AdminPage
      title="مدیریت قیمت‌ها"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: 'ویرایش محصول', href: `/products/${id}` },
        { label: 'مدیریت قیمت‌ها' },
      ]}
    >
      <PricingPanel product={product} variantPriced={skus.some((sku) => sku.active)} />
    </AdminPage>
  );
}
