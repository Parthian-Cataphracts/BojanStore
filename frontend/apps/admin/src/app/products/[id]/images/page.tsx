import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { ProductImagesPanel } from '@/components/product/ProductImagesPanel';
import { getProduct } from '@/lib/api/products';

export const metadata: Metadata = { title: 'مدیریت تصاویر محصول' };

/** Screen 105 — مدیریت تصاویر محصول. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const product = await getProduct(id);

  if (!product) notFound();

  // `images` is the whole gallery, primary first. Older responses carried only
  // the primary, so fall back to it rather than showing an empty gallery for a
  // product that has a picture.
  const images = product.images ?? (product.image ? [product.image] : []);

  return (
    <AdminPage
      title="مدیریت تصاویر محصول"
      breadcrumbs={[
        { label: 'محصولات', href: '/products' },
        { label: product.title, href: `/products/${id}` },
        { label: 'مدیریت تصاویر محصول' },
      ]}
    >
      <ProductImagesPanel productId={id} images={images} />
    </AdminPage>
  );
}
