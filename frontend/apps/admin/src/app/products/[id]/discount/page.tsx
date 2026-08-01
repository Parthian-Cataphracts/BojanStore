import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { DiscountPanel } from '@/components/product/DiscountPanel';
import { getProduct } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت تخفیف محصول' };

/** Screen 110 — مدیریت تخفیف محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const product = await getProduct(id);

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
      <DiscountPanel product={product} />
    </AdminPage>
  );
}
