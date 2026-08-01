import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { ProductForm } from '@/components/ProductForm';
import { getBrands, getCategories } from '@/lib/api/catalogue';

export const metadata: Metadata = { title: 'افزودن محصول جدید' };

/** Screen 97 — Add a product. */
export default async function NewProductPage() {
  const [brands, categories] = await Promise.all([getBrands(), getCategories()]);

  return (
    <AdminPage
      title="افزودن محصول جدید"
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'محصولات', href: '/products' },
        { label: 'افزودن محصول' },
      ]}
    >
      <ProductForm brands={brands} categories={categories} />
    </AdminPage>
  );
}
