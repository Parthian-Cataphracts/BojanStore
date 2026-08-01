import type { Metadata } from 'next';
import { ThemedListing } from '@/components/product/ThemedListing';
import { getProducts } from '@/lib/api/catalog';

export const metadata: Metadata = {
  title: 'محصولات جدید',
  description: 'تازه‌ترین محصولاتی که به فروشگاه بوژان اضافه شده‌اند.',
};

/** Screen 23 — محصولات جدید. */
export default async function Page() {
  const { items } = await getProducts({ sort: 'newest', pageSize: 24 });

  return (
    <ThemedListing
      title="محصولات جدید"
      intro="تازه‌ها را زودتر از بقیه ببینید؛ این فهرست با هر بار افزودن محصول جدید بروزرسانی می‌شود."
      products={items}
    />
  );
}
