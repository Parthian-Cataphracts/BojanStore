import type { Metadata } from 'next';
import { SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductGrid, ProductRail } from '@/components/product/ProductGrid';
import { getBestsellers, getProducts } from '@/lib/api/catalog';
import { getRecentlyViewed } from '@/lib/api/activity';

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
  title: 'پیشنهادهای ویژه برای شما',
  description: 'مجموعه‌ای انتخاب‌شده بر اساس سلیقه، بازدیدهای اخیر و خریدهای گذشته شما.',
};

/** Screen 89 — Personalised offers. */
export default async function OffersPage() {
  const [{ items }, viewed, bestsellers] = await Promise.all([
    getProducts({ pageSize: 24 }),
    getRecentlyViewed(),
    getBestsellers(8),
  ]);

  // "Offers" are the discounted products; the rest of the page is discovery.
  const discounted = items.filter((product) => product.compareAtPrice);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <header className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-page-title">
          پیشنهادهای ویژه برای شما
        </h1>
        <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant">
          مجموعه‌ای با دقت انتخاب‌شده بر اساس سلیقه، بازدیدهای اخیر و خریدهای گذشته شما.
        </p>
      </header>

      <section className="flex flex-col gap-lg">
        <SectionHeader title="تخفیف‌های فعال" subtitle="تا زمانی که موجودی هست" />
        <ProductGrid products={discounted} />
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title="بر اساس بازدیدهای اخیر شما" />
        <ProductRail products={viewed} />
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title="محبوب بین مشتریان بوژان" />
        <ProductRail products={bestsellers} />
      </section>
    </Container>
  );
}
