import Image from 'next/image';
import Link from 'next/link';
import { buttonClasses, Icon, SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductRail } from '@/components/product/ProductGrid';
import { getBestsellers, getCategories, getNewArrivals } from '@/lib/api/catalog';
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

/**
 * Design mock imagery, same host as the rest of the placeholder media. Replace
 * with the store's own CDN once the backend serves hero art — the host is
 * already allowed in `next.config.mjs` and the content policy.
 */
const HERO_IMAGE =
  'https://lh3.googleusercontent.com/aida-public/AB6AXuD0bLSDR3LG058UKuwpzAND3hPxtrZl_fnR9OZMvPFbuWfchZT2nIFuMkLMa1g8FavBFsmow9lF50Q4CI2FrnweZ_0-jKLI_VdNNpprV-Of3tCqQh-DseezhHVCRzCZo5r0DgdXGNeYfiliw5K46ogWF3aPb_GBUQSrTmrrDCOjZPk0C-kFwSfmXLCUD33gKy5fiDCuYkMc5XPXv5zwDAo-c3njSZJ90ZOqlOiZCHByrMkn5oGvXzNfPofx8mlN4avRWvKIzOcrXfY';

/** Screen 01 — Home. */
export default async function HomePage() {
  const [categories, newArrivals, bestsellers] = await Promise.all([
    getCategories(),
    getNewArrivals(8),
    getBestsellers(8),
  ]);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      {/*
        Hero — 400px on mobile, 600px on desktop, per the two design
        variants, but `min-h` rather than `h`: a fixed height clipped the
        overlay card's own content (title + intro + CTA) whenever it grew
        past that height on a narrow screen, cutting the button off with no
        visible sign anything was missing.
      */}
      <section className="paper-card relative flex min-h-[400px] items-end overflow-hidden rounded-xl p-lg shadow-soft md:min-h-[600px] md:items-center md:justify-center md:p-xl md:text-center">
        {/*
          The hero is the largest thing on the page and so almost always the
          LCP element. As a CSS `background-image` it could not be found until
          the stylesheet had parsed, and it arrived at full size in its original
          format. `next/image` emits a preload for it, serves AVIF/WebP, and
          picks a width from `sizes` — the same picture, a fraction of the bytes,
          requested at the start of the page load rather than the middle.
        */}
        <Image
          src={HERO_IMAGE}
          alt=""
          fill
          priority
          sizes="100vw"
          className="object-cover opacity-80 mix-blend-multiply"
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
