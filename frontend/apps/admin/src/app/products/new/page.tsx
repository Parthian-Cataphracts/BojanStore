import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { ProductForm } from '@/components/ProductForm';
import { getBrands, getCategories, getCollections } from '@/lib/api/catalogue';

export const metadata: Metadata = { title: 'افزودن محصول جدید' };

/** Screen 97 — Add a product. */
export default async function NewProductPage() {
  const [brands, categories, collections] = await Promise.all([
    getBrands(),
    getCategories(),
    getCollections(),
  ]);

  return (
    <AdminPage
      title="افزودن محصول جدید"
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'محصولات', href: '/products' },
        { label: 'افزودن محصول' },
      ]}
    >
      <ProductForm brands={brands} categories={categories} collections={collections} />
    </AdminPage>
  );
}
