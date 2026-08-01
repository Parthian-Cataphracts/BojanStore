import type { Metadata } from 'next';
import { ThemedListing } from '@/components/product/ThemedListing';
import { getProducts } from '@/lib/api/catalog';

export const metadata: Metadata = {
  title: 'محصولات پرفروش',
  description: 'پرفروش‌ترین محصولات فروشگاه بوژان بر اساس انتخاب مشتریان.',
};

/** Screen 24 — محصولات پرفروش. */
export default async function Page() {
  const { items } = await getProducts({ sort: 'bestselling', pageSize: 24 });

  return (
    <ThemedListing
      title="محصولات پرفروش"
      intro="انتخاب محبوب مشتریان بوژان؛ محصولاتی که بیشترین خرید را داشته‌اند."
      products={items}
    />
  );
}
