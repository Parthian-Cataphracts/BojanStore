import type { Metadata } from 'next';
import Link from 'next/link';
import { buttonClasses, Icon, SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductRail } from '@/components/product/ProductGrid';
import { getBestsellers, getCategories, getNewArrivals } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  alternates: { canonical: routes.home },
};

/** Screen 01 — Home. */
export default async function HomePage() {
  const [categories, newArrivals, bestsellers] = await Promise.all([
    getCategories(),
    getNewArrivals(8),
    getBestsellers(8),
  ]);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      {/* Hero — 400px on mobile, 600px on desktop, per the two design variants. */}
      <section className="paper-card relative flex h-[400px] items-end overflow-hidden rounded-xl p-lg shadow-soft md:h-[600px] md:items-center md:justify-center md:p-xl md:text-center">
        <div
          className="absolute inset-0 bg-cover bg-center opacity-80 mix-blend-multiply"
          style={{
            backgroundImage:
              "url('https://lh3.googleusercontent.com/aida-public/AB6AXuD0bLSDR3LG058UKuwpzAND3hPxtrZl_fnR9OZMvPFbuWfchZT2nIFuMkLMa1g8FavBFsmow9lF50Q4CI2FrnweZ_0-jKLI_VdNNpprV-Of3tCqQh-DseezhHVCRzCZo5r0DgdXGNeYfiliw5K46ogWF3aPb_GBUQSrTmrrDCOjZPk0C-kFwSfmXLCUD33gKy5fiDCuYkMc5XPXv5zwDAo-c3njSZJ90ZOqlOiZCHByrMkn5oGvXzNfPofx8mlN4avRWvKIzOcrXfY')",
          }}
        />

        <div className="relative z-10 w-full rounded-lg bg-surface/90 p-md backdrop-blur-md md:flex md:max-w-2xl md:flex-col md:items-center md:gap-gutter md:rounded-xl md:border md:border-outline-variant/20 md:bg-surface-container-lowest/80 md:p-xl">
          <h1 className="mb-sm font-headline text-headline-xl text-primary-container">
            بوژان، برای لحظه‌های خلاق زندگی
          </h1>
          <p className="mb-lg text-body-md leading-relaxed text-on-surface-variant md:mb-0 md:max-w-xl md:text-body-lg">
            از نوشت‌افزار و دفتر تا ابزار هنری، معماری، هدیه و اکسسوری‌های خاص.
          </p>

          {/* Rendered as a link so the hero CTA stays crawlable. */}
          <Link
            href={routes.products}
            className={buttonClasses({
              fullWidth: true,
              className: 'md:mt-md md:h-14 md:w-auto md:rounded-full md:px-xl',
            })}
          >
            مشاهده محصولات
          </Link>
        </div>
      </section>

      {/* Search entry point — mobile only; desktop uses the header icon. */}
      <section className="md:hidden">
        <Link
          href={routes.search}
          className="flex h-12 items-center gap-sm rounded-lg border-b border-surface-variant bg-soft-mint/30 px-md"
        >
          <Icon name="search" className="text-primary" />
          <span className="text-body-md text-outline">دنبال چی می‌گردی؟</span>
        </Link>
      </section>

      {/* Popular categories */}
      <section className="flex flex-col gap-lg">
        <SectionHeader
          title="دسته‌بندی‌های محبوب"
          actionLabel="همه دسته‌بندی‌ها"
          actionHref={routes.categories}
        />

        <div className="grid grid-cols-2 gap-gutter md:grid-cols-4">
          {categories.slice(0, 4).map((category) => (
            <Link
              key={category.slug}
              href={routes.category(category.slug)}
              className="paper-card group flex flex-col items-center gap-sm rounded-lg p-md text-center transition-shadow hover:shadow-soft"
            >
              <span className="mb-sm flex h-16 w-16 items-center justify-center rounded-full bg-primary-fixed-dim/20 transition-transform group-hover:scale-110">
                <Icon name={category.icon} size={30} className="text-primary-container" />
              </span>
              <span className="text-label-md font-label-md text-primary-container">
                {category.name}
              </span>
            </Link>
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader
          title="جدیدترین محصولات"
          subtitle="تازه‌ها را زودتر از بقیه ببینید"
          actionLabel="مشاهده همه"
          actionHref={`${routes.products}?sort=newest`}
        />
        <ProductRail products={newArrivals} />
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader
          title="پرفروش‌ترین‌ها"
          subtitle="انتخاب محبوب مشتریان بوژان"
          actionLabel="مشاهده همه"
          actionHref={`${routes.products}?sort=bestselling`}
        />
        <ProductRail products={bestsellers} />
      </section>
    </Container>
  );
}
