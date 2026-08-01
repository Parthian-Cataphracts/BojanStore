import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SkuTable } from '@/components/product/SkuTable';

export const metadata: Metadata = { title: 'مدیریت SKU' };

/** Screen 108 — مدیریت SKU. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  return (
    <AdminPage
      title="مدیریت SKU"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: 'ویرایش محصول', href: `/products/${id}` },
        { label: 'مدیریت SKU' },
      ]}
    >
      <SkuTable />
    </AdminPage>
  );
}
