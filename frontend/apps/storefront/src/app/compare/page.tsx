import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Card, EmptyState, formatPrice, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { CompareControls } from '@/components/product/CompareControls';
import { getCompareProducts } from '@/lib/api/activity';
import { getProducts } from '@/lib/api/catalog';
import type { Product } from '@/lib/api/types';
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
  title: 'مقایسه محصولات',
  robots: { index: false },
};

/** Attribute rows drawn on screen 60, resolved per product. */
const rows: { label: string; value: (product: Product) => string }[] = [
  { label: 'قیمت', value: (product) => formatPrice(product.price) },
  { label: 'برند', value: (product) => product.brand },
  { label: 'دسته‌بندی', value: (product) => product.categoryName },
  { label: 'امتیاز', value: (product) => `${toPersianDigits(product.rating)} از ۵` },
  { label: 'تعداد نظر', value: (product) => toPersianDigits(product.reviewCount) },
  {
    label: 'موجودی',
    value: (product) =>
      product.stock > 0 ? `${toPersianDigits(product.stock)} عدد موجود` : 'ناموجود',
  },
];

/** Screen 60 — Product comparison. */
export default async function ComparePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const slugs = (first(params.slugs) ?? '').split(',').filter(Boolean);
  const [products, catalogue] = await Promise.all([
    getCompareProducts(slugs),
    // The picker needs something to pick from; only the four fields it shows.
    getProducts({ pageSize: 100 }),
  ]);

  // What the comparison actually holds, in case a slug in the URL matched
  // nothing — otherwise removing a column would silently re-add the bad one.
  const activeSlugs = products.map((product) => product.slug);
  const candidates = catalogue.items.map((product) => ({
    slug: product.slug,
    title: product.title,
    image: product.image,
    brand: product.brand,
  }));

  if (products.length === 0) {
    return (
      <Container className="py-lg md:py-xl">
        <PageHeader title="مقایسه محصولات" backHref={routes.products} />
        <EmptyState
          icon="compare_arrows"
          title="محصولی برای مقایسه انتخاب نشده"
          description="محصولاتی را که می‌خواهید کنار هم ببینید، اضافه کنید."
          action={
            <CompareControls slugs={[]} candidates={candidates} slot="add" />
          }
        />
      </Container>
    );
  }

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="مقایسه محصولات"
        backHref={routes.products}
        subtitle={`${toPersianDigits(products.length)} محصول در مقایسه`}
      />

      {/* The table scrolls inside its own container so the page never does. */}
      <Card surface="plain" className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[560px] border-collapse text-start text-body-md">
            <caption className="sr-only">مقایسه ویژگی‌های محصولات انتخاب‌شده</caption>

            <thead>
              <tr className="border-b border-outline-variant/40">
                <th
                  scope="col"
                  className="sticky start-0 z-10 bg-surface-container-low px-lg py-md text-start text-caption font-normal text-on-surface-variant"
                >
                  ویژگی‌ها
                </th>

                {products.map((product) => (
                  <th key={product.slug} scope="col" className="p-lg align-top font-normal">
                    <div className="relative flex w-40 flex-col gap-sm">
                      <CompareControls
                        slugs={activeSlugs}
                        candidates={candidates}
                        slot="remove"
                        removeLabel={{ slug: product.slug, title: product.title }}
                      />
                      <Link
                        href={routes.product(product.slug)}
                        className="relative block aspect-square w-full overflow-hidden rounded-lg border border-outline-variant bg-surface-container-highest"
                      >
                        <Image
                          src={product.image}
                          alt={product.title}
                          fill
                          sizes="160px"
                          className="object-cover"
                        />
                      </Link>
                      <Link
                        href={routes.product(product.slug)}
                        className="line-clamp-2 text-start text-label-md font-label-md text-primary transition-colors hover:text-secondary"
                      >
                        {product.title}
                      </Link>
                    </div>
                  </th>
                ))}
              </tr>
            </thead>

            <tbody>
              {rows.map((row) => (
                <tr key={row.label} className="border-b border-outline-variant/30 last:border-0">
                  <th
                    scope="row"
                    className="sticky start-0 z-10 bg-surface-container-low px-lg py-md text-start text-caption font-normal text-on-surface-variant"
                  >
                    {row.label}
                  </th>
                  {products.map((product) => (
                    <td key={product.slug} className="tabular px-lg py-md text-on-surface">
                      {row.value(product)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <CompareControls slugs={activeSlugs} candidates={candidates} slot="add" />
    </Container>
  );
}
