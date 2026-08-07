import type { Metadata } from 'next';
import { ThemedListing } from '@/components/product/ThemedListing';
import { getProducts } from '@/lib/api/catalog';

/*
 * Rendered on request, not at build.
 *
 * This page reads the catalogue, and the catalogue lives behind the API — which
 * does not exist when the image is built. Prerendering it meant `next build`
 * fetching from a host that is not up yet, which is exactly how the Docker
 * build failed. The alternative, emitting it with whatever an unreachable API
 * returns, is worse: the first visitors after a deploy would be served an empty
 * shop until the first revalidation filled it in.
 *
 * Nothing is lost by it. The fetches underneath already declare their own
 * `revalidate` window, so the API is not called per request either way — the
 * caching just happens a layer down, where stock and prices can expire on their
 * own schedule instead of being frozen into the image.
 */
export const dynamic = 'force-dynamic';

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
