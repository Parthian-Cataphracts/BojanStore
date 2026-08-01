import type { Metadata } from 'next';
import { Suspense } from 'react';
import { toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { CatalogToolbar } from '@/components/product/CatalogToolbar';
import { CategoryChips } from '@/components/product/CategoryChips';
import { NoResults } from '@/components/product/NoResults';
import { Pagination } from '@/components/product/Pagination';
import { ProductGrid } from '@/components/product/ProductGrid';
import { getBrands, getCategories, getProducts } from '@/lib/api/catalog';
import { toProductQuery, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'محصولات',
  description: 'فهرست کامل محصولات بوژان با امکان فیلتر و مرتب‌سازی.',
};

/** Screen 04 — Product list. */
export default async function ProductsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const query = toProductQuery(params);

  const [{ items, total, page, pageSize }, brands, categories] = await Promise.all([
    getProducts(query),
    getBrands(),
    getCategories(),
  ]);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <header className="flex flex-col gap-md">
        <div className="flex items-center justify-between gap-md">
          <h1 className="font-headline text-display-md text-on-surface">همه محصولات</h1>
          <span className="tabular shrink-0 text-caption text-outline">
            {toPersianDigits(total)} محصول
          </span>
        </div>

        <Suspense fallback={null}>
          <CatalogToolbar brands={brands} />
        </Suspense>
      </header>

      <CategoryChips categories={categories} {...(query.category ? { activeSlug: query.category } : null)} />

      {items.length > 0 ? (
        <>
          <ProductGrid products={items} />
          <Pagination
            page={page}
            pageSize={pageSize}
            total={total}
            params={params}
            basePath={routes.products}
          />
        </>
      ) : (
        <NoResults clearHref={routes.products} categories={categories} />
      )}
    </Container>
  );
}
