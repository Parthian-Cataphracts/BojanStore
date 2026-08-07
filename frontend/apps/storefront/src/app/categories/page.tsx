import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { getCategories } from '@/lib/api/catalog';
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
  title: 'دسته‌بندی محصولات',
  description: 'همه دسته‌بندی‌های فروشگاه بوژان؛ از نوشت‌افزار و دفتر تا ابزار هنری و هدیه.',
};

/** Screen 02 — Product categories. */
export default async function CategoriesPage() {
  const categories = await getCategories();

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <header className="flex flex-col gap-xs">
        <h1 className="font-headline text-display-md text-primary md:text-headline-lg">
          دسته‌بندی محصولات
        </h1>
        <p className="text-body-md text-on-surface-variant">
          {toPersianDigits(categories.length)} دسته‌بندی اصلی برای کشف کردن
        </p>
      </header>

      <div className="flex flex-col gap-lg">
        {categories.map((category) => (
          <section key={category.slug} className="paper-card rounded-xl p-lg shadow-ambient">
            <div className="flex items-center justify-between gap-md">
              <Link
                href={routes.category(category.slug)}
                className="group flex items-center gap-md"
              >
                <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary-fixed-dim/20 transition-transform group-hover:scale-110">
                  <Icon name={category.icon} size={28} className="text-primary-container" />
                </span>
                <span className="flex flex-col gap-xs">
                  <span className="text-body-lg font-label-md text-primary-container">
                    {category.name}
                  </span>
                  <span className="tabular text-caption text-on-surface-variant">
                    {toPersianDigits(category.productCount)} محصول
                  </span>
                </span>
              </Link>

              <Link
                href={routes.category(category.slug)}
                className="inline-flex shrink-0 items-center gap-xs text-label-md font-label-md text-secondary transition-colors hover:text-primary"
              >
                مشاهده
                <Icon name="chevron_left" size={18} />
              </Link>
            </div>

            {category.children && category.children.length > 0 && (
              <div className="mt-lg flex flex-wrap gap-sm border-t border-paper-border pt-lg">
                {category.children.map((child) => (
                  <Link
                    key={child.slug}
                    href={`${routes.category(category.slug)}?sub=${child.slug}`}
                    className="rounded-full border border-outline-variant bg-surface-container px-md py-sm text-label-md font-label-md text-on-surface transition-colors hover:bg-surface-variant"
                  >
                    {child.name}
                  </Link>
                ))}
              </div>
            )}
          </section>
        ))}
      </div>
    </Container>
  );
}
