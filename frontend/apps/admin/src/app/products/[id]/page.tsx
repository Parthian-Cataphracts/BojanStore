import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { ProductForm } from '@/components/ProductForm';
import { getBrands, getCategories, getCollections } from '@/lib/api/catalogue';
import { getProduct, getProductSkus } from '@/lib/api/products';

export const metadata: Metadata = { title: 'ویرایش محصول' };

/** Screens 98 and 105-110 — Edit a product. */
export default async function EditProductPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  /*
    The SKUs decide whether the base price is still the price: a product that
    sells by combination is charged from the combination, so the field on this
    form stops being something the operator has to fill in.

    Caught, unlike everywhere else this is read. `getProductSkus` treats a
    failure as a failure because on screen 108 the list *is* the screen — but
    here it only relaxes one field, and letting it throw would mean an operator
    could not correct a product's title because an unrelated read failed. Empty
    is the safe direction: the price stays required, which is what it was
    before this flag existed.
  */
  const [product, brands, categories, collections, skus] = await Promise.all([
    getProduct(id),
    getBrands(),
    getCategories(),
    getCollections(),
    getProductSkus(id).catch(() => []),
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
        variantPriced={skus.some((sku) => sku.active && sku.price > 0)}
      />
    </AdminPage>
  );
}
