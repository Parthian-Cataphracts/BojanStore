import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { PricingPanel } from '@/components/product/PricingPanel';
import { getProduct } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت قیمت‌ها' };

/** Screen 109 — مدیریت قیمت‌ها. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const product = await getProduct(id);

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
      <PricingPanel product={product} />
    </AdminPage>
  );
}
