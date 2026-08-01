import type { Metadata } from 'next';
import { ThemedListing } from '@/components/product/ThemedListing';
import { getProducts } from '@/lib/api/catalog';

export const metadata: Metadata = {
  title: 'هدیه و سبک زندگی',
  description: 'اقلام خاص و بسته‌بندی‌شده برای هدیه دادن و زیباتر کردن فضای زندگی.',
};

/** Screen 25 — هدیه و سبک زندگی. */
export default async function Page() {
  const { items } = await getProducts({ category: 'gift-lifestyle', pageSize: 24 });

  return (
    <ThemedListing
      title="هدیه و سبک زندگی"
      intro="چیزهایی که دادنشان لذت‌بخش است: سرامیک دست‌ساز، دکور خانه و باکس‌های هدیه."
      products={items}
    />
  );
}
