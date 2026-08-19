import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { ProductForm } from '@/components/ProductForm';
import { getBrands, getCategories, getCollections } from '@/lib/api/catalogue';
import { getProduct } from '@/lib/api/products';

export const metadata: Metadata = { title: 'ویرایش محصول' };

/** Screens 98 and 105-110 — Edit a product. */
export default async function EditProductPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const [product, brands, categories, collections] = await Promise.all([
    getProduct(id),
    getBrands(),
    getCategories(),
    getCollections(),
  ]);
  if (!product) notFound();

  return (
    <AdminPage
      title="ویرایش محصول"
      description={product.title}
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'محصولات', href: '/products' },
        { label: product.title },
      ]}
    >
      <ProductForm
        product={product}
        brands={brands}
        categories={categories}
        collections={collections}
      />
    </AdminPage>
  );
}
