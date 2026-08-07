import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Suspense } from 'react';
import { Breadcrumb, EmptyState, buttonClasses, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { CatalogToolbar } from '@/components/product/CatalogToolbar';
import { Pagination } from '@/components/product/Pagination';
import { ProductGrid } from '@/components/product/ProductGrid';
import { getBrands, getCategories, getCategory, getProducts } from '@/lib/api/catalog';
import { toProductQuery, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export async function generateStaticParams() {
  const categories = await getCategories();
  return categories.map((category) => ({ slug: category.slug }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const category = await getCategory(slug);
  if (!category) return { title: 'دسته‌بندی' };

  return {
    title: category.name,
    description: `خرید ${category.name} از فروشگاه بوژان با تنوع کامل و ارسال سریع.`,
    alternates: { canonical: routes.category(category.slug) },
  };
}

/** Screen 03 — Category detail. */
export default async function CategoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ slug: string }>;
  searchParams: Promise<SearchParams>;
}) {
  const [{ slug }, rawSearchParams] = await Promise.all([params, searchParams]);

  const category = await getCategory(slug);
  if (!category) notFound();

  // The subcategory chips post `?sub=`, and it used to reach nothing but the
  // chip highlighting — the query was pinned to the parent slug, so picking
  // "دکور خانه" lit that chip up and went on showing every product in the
  // parent. A filter that changes only its own appearance is worse than no
  // filter: it says the list in front of you is narrower than it is.
  //
  // Only a slug this category actually has as a child is honoured. Anything
  // else in the URL falls back to the parent rather than filtering to nothing.
  // Only children that actually hold something are offered. The taxonomy has
  // subcategories nothing is assigned to — every gift-lifestyle product sits on
  // the parent — and a chip that filters correctly to an empty page is still a
  // dead end the shopper had no way to see coming.
  const subcategories = (category.children ?? []).filter((child) => child.productCount > 0);

  const requestedSub = typeof rawSearchParams.sub === 'string' ? rawSearchParams.sub : undefined;
  const activeSub = subcategories.some((child) => child.slug === requestedSub)
    ? requestedSub
    : undefined;

  const activeSubName = subcategories.find((child) => child.slug === activeSub)?.name;

  const query = { ...toProductQuery(rawSearchParams), category: activeSub ?? slug };
  const [{ items, total, page, pageSize }, brands] = await Promise.all([
    getProducts(query),
    getBrands(),
  ]);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <Breadcrumb
        items={[
          { label: 'خانه', href: routes.home },
          { label: 'دسته‌بندی‌ها', href: routes.categories },
          { label: category.name },
        ]}
      />

      <header className="flex flex-col gap-md">
        <div className="flex items-center justify-between gap-md">
          <h1 className="font-headline text-display-md text-on-surface md:text-headline-lg">
            {category.name}
          </h1>
          <span className="tabular shrink-0 text-caption text-outline">
            {toPersianDigits(total)} محصول
          </span>
        </div>

        <Suspense fallback={null}>
          <CatalogToolbar brands={brands} />
        </Suspense>
      </header>

      {/* Subcategory chips */}
      {subcategories.length > 0 && (
        <nav
          aria-label="زیر‌دسته‌ها"
          className="hide-scrollbar -mx-margin-mobile flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
        >
          <Link
            href={routes.category(category.slug)}
            className={`whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors ${
              activeSub
                ? 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant'
                : 'bg-primary-fixed text-on-primary-fixed'
            }`}
          >
            همه
          </Link>
          {subcategories.map((child) => (
            <Link
              key={child.slug}
              href={`${routes.category(category.slug)}?sub=${child.slug}`}
              className={`whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors ${
                activeSub === child.slug
                  ? 'bg-primary-fixed text-on-primary-fixed'
                  : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant'
              }`}
            >
              {child.name}
            </Link>
          ))}
        </nav>
      )}

      {items.length > 0 ? (
        <>
          <ProductGrid products={items} />
          <Pagination
            page={page}
            pageSize={pageSize}
            total={total}
            params={rawSearchParams}
            basePath={routes.category(category.slug)}
          />
        </>
      ) : (
        <EmptyState
          icon="inventory_2"
          title={`فعلاً محصولی در ${activeSubName ?? category.name} نیست`}
          description="به‌زودی محصولات این دسته‌بندی اضافه می‌شوند. در این فاصله سایر دسته‌ها را ببینید."
          action={
            <Link href={routes.categories} className={buttonClasses()}>
              سایر دسته‌بندی‌ها
            </Link>
          }
        />
      )}
    </Container>
  );
}
