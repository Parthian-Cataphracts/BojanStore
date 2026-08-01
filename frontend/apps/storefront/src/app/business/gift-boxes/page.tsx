import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Badge, Card, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ALL_BUNDLES, getGiftBundleCategories, getGiftBundles } from '@/lib/api/business';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'بسته‌های هدیه شرکتی',
  description: 'هدایای نفیس و کاربردی برای قدردانی از همکاران و شرکای تجاری.',
};

/** Screen 66 — Corporate gift bundles. */
export default async function GiftBoxesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const category = first(params.category) ?? ALL_BUNDLES;

  // The tabs are the categories the bundles are actually in, not a fixed list
  // of four — a bundle filed under anything else used to be reachable only by
  // typing the category into the URL, and a category that lost its last bundle
  // stayed on the page as a tab leading to an empty grid.
  const [categories, bundles] = await Promise.all([
    getGiftBundleCategories(),
    getGiftBundles(category),
  ]);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="بسته‌های هدیه شرکتی"
        backHref={routes.business}
        subtitle="مجموعه‌ای از هدایای نفیس و کاربردی، طراحی‌شده برای قدردانی از همکاران و شرکای تجاری شما."
      />

      <nav
        aria-label="دسته‌بندی بسته‌ها"
        className="hide-scrollbar -mx-margin-mobile flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
      >
        {categories.map((name) => (
          <Link
            key={name}
            href={`${routes.businessGiftBoxes}?category=${encodeURIComponent(name)}`}
            className={`shrink-0 whitespace-nowrap rounded-full px-md py-sm text-label-md font-medium transition-colors ${
              category === name
                ? 'bg-primary-fixed text-on-primary-fixed'
                : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant'
            }`}
          >
            {name}
          </Link>
        ))}
      </nav>

      <div className="grid gap-gutter sm:grid-cols-2 lg:grid-cols-3">
        {bundles.map((bundle) => (
          <Card key={bundle.slug} className="flex flex-col overflow-hidden">
            <div className="relative aspect-[4/3] w-full border-b border-paper-border bg-surface-container-highest">
              <Image
                src={bundle.cover}
                alt={bundle.title}
                fill
                sizes="(max-width: 640px) 100vw, 33vw"
                className="object-cover"
              />
              <span className="absolute end-sm top-sm">
                <Badge tone="mint">{bundle.category}</Badge>
              </span>
            </div>

            <div className="flex flex-1 flex-col gap-sm p-lg">
              <h2 className="text-card-title text-primary">{bundle.title}</h2>
              <p className="text-body-md leading-relaxed text-on-surface-variant">
                {bundle.summary}
              </p>

              <dl className="mt-auto flex flex-col gap-xs pt-md text-caption">
                <div className="flex items-center justify-between">
                  <dt className="text-on-surface-variant">قیمت هر بسته</dt>
                  <dd className="tabular font-semibold text-primary">
                    {formatPrice(bundle.pricePerUnit)}
                  </dd>
                </div>
                <div className="flex items-center justify-between">
                  <dt className="text-on-surface-variant">حداقل سفارش</dt>
                  <dd className="tabular text-on-surface">
                    {toPersianDigits(bundle.minimumQuantity)} عدد
                  </dd>
                </div>
              </dl>

              <Link
                href={routes.businessQuote}
                className={buttonClasses({ size: 'sm', fullWidth: true, className: 'mt-md' })}
              >
                درخواست پیش‌فاکتور
              </Link>
            </div>
          </Card>
        ))}
      </div>
    </Container>
  );
}
