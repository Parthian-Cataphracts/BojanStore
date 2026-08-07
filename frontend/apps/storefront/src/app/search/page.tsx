import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { SectionHeader, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { NoResults } from '@/components/product/NoResults';
import { Pagination } from '@/components/product/Pagination';
import { ProductGrid, ProductRail } from '@/components/product/ProductGrid';
import { ProductGridFallback } from '@/components/product/ProductGridFallback';
import { SearchBar } from '@/components/search/SearchBar';
import { RecordSearch } from '@/components/search/RecordSearch';
import { SearchHistory } from '@/components/search/SearchHistory';
import { getBestsellers, getCategories, getProducts } from '@/lib/api/catalog';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

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
  title: 'جستجو',
  description: 'جستجو در میان محصولات بوژان.',
  robots: { index: false },
};

const popularTerms = ['آبرنگ', 'دفتر طراحی', 'قلم‌مو', 'پلنر', 'خودنویس', 'گلدان سفالی'];

/** Screen 05 — Search. */
export default async function SearchPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const term = first(params.q)?.trim() ?? '';

  const [results, categories, suggestions] = await Promise.all([
    term
      ? getProducts({ search: term, pageSize: 24, page: Number(first(params.page) ?? 1) || 1 })
      : Promise.resolve(null),
    getCategories(),
    getBestsellers(8),
  ]);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      {term && <RecordSearch term={term} hits={results?.items.length ?? 0} />}
      <div className="flex flex-col gap-md">
        <h1 className="font-headline text-display-md text-primary">جستجو در بوژان</h1>
        <Suspense fallback={<div className="h-12 rounded-lg bg-surface-container" />}>
          <SearchBar autoFocus={!term} />
        </Suspense>
      </div>

      {results ? (
        <Suspense fallback={<ProductGridFallback />}>
          <section className="flex flex-col gap-lg">
            <div className="flex items-baseline justify-between gap-md">
              <h2 className="text-body-lg font-label-md text-on-surface">
                نتایج جستجو برای «{term}»
              </h2>
              <span className="tabular shrink-0 text-caption text-outline">
                {toPersianDigits(results.total)} مورد
              </span>
            </div>

            {results.items.length > 0 ? (
              <>
                <ProductGrid products={results.items} />
                <Pagination
                  page={results.page}
                  pageSize={results.pageSize}
                  total={results.total}
                  params={params}
                  basePath={routes.search}
                />
              </>
            ) : (
              <NoResults
                title="نتیجه‌ای پیدا نشد"
                description={`برای «${term}» محصولی پیدا نکردیم. املای عبارت را بررسی کنید یا از دسته‌بندی‌ها شروع کنید.`}
                clearHref={routes.search}
                categories={categories}
              />
            )}
          </section>
        </Suspense>
      ) : (
        <>
          {/* Screen 90 — recent searches, cleared client-side. */}
          <SearchHistory />

          <section className="flex flex-col gap-md">
            <h2 className="text-label-md font-label-md text-primary">جستجوهای پرطرفدار</h2>
            <div className="flex flex-wrap gap-sm">
              {popularTerms.map((popular) => (
                <Link
                  key={popular}
                  href={`${routes.search}?q=${encodeURIComponent(popular)}`}
                  className="rounded-full border border-outline-variant bg-surface-container px-md py-sm text-label-md font-label-md text-on-surface transition-colors hover:bg-surface-variant"
                >
                  {popular}
                </Link>
              ))}
            </div>
          </section>

          <section className="flex flex-col gap-md">
            <h2 className="text-label-md font-label-md text-primary">دسته‌بندی‌ها</h2>
            <div className="flex flex-wrap gap-sm">
              {categories.map((category) => (
                <Link
                  key={category.slug}
                  href={routes.category(category.slug)}
                  className="rounded-full bg-soft-mint px-md py-sm text-label-md font-label-md text-primary transition-opacity hover:opacity-80"
                >
                  {category.name}
                </Link>
              ))}
            </div>
          </section>

          <section className="flex flex-col gap-lg">
            <SectionHeader title="شاید این‌ها را بپسندید" />
            <ProductRail products={suggestions} />
          </section>
        </>
      )}
    </Container>
  );
}
