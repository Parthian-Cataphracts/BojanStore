import Image from 'next/image';
import Link from 'next/link';
import { buttonClasses, cn, Icon, SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ArticleCard } from '@/components/magazine/ArticleCard';
import { HomeFaq } from '@/components/home/HomeFaq';
import { HomeTestimonials } from '@/components/home/HomeTestimonials';
import { ProductRail } from '@/components/product/ProductGrid';
import { getBestsellers, getCategories, getNewArrivals } from '@/lib/api/catalog';
import { getHomeArticles, getTestimonials } from '@/lib/api/editorial';
import { bannerSlugs, getBanner, getFaqs } from '@/lib/api/pages';
import { getStoreSettings } from '@/lib/api/store';
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
 * The hero the shop launches with, until an operator sets one.
 *
 * Design mock imagery, same host as the rest of the placeholder media — the
 * host is already allowed in `next.config.mjs` and the content policy. A banner
 * saved in محتوا ← بنرها under the slug `home-hero` replaces all three of these.
 */
const HERO_TITLE = 'بوژان، برای لحظه‌های خلاق زندگی';
const HERO_SUBTITLE = 'از نوشت‌افزار و دفتر تا ابزار هنری، معماری، هدیه و اکسسوری‌های خاص.';
const HERO_IMAGE =
  'https://lh3.googleusercontent.com/aida-public/AB6AXuD0bLSDR3LG058UKuwpzAND3hPxtrZl_fnR9OZMvPFbuWfchZT2nIFuMkLMa1g8FavBFsmow9lF50Q4CI2FrnweZ_0-jKLI_VdNNpprV-Of3tCqQh-DseezhHVCRzCZo5r0DgdXGNeYfiliw5K46ogWF3aPb_GBUQSrTmrrDCOjZPk0C-kFwSfmXLCUD33gKy5fiDCuYkMc5XPXv5zwDAo-c3njSZJ90ZOqlOiZCHByrMkn5oGvXzNfPofx8mlN4avRWvKIzOcrXfY';

/** Screen 01 — Home. */
export default async function HomePage() {
  const [categories, newArrivals, bestsellers, banner, testimonials, articles, faqs, settings] =
    await Promise.all([
      getCategories(),
      getNewArrivals(8),
      getBestsellers(8),
      getBanner(bannerSlugs.homeHero),
      getTestimonials(6),
      getHomeArticles(3),
      getFaqs(),
      getStoreSettings(),
    ]);

  /*
    Fetched alongside the content rather than gating the fetch.

    Reading the switches first and only then asking for what is switched on
    would turn one round of parallel requests into two, and would put the
    settings call on the critical path of the shop's most-visited page. The
    three fetches are cached reads of small lists; the cost of making them and
    discarding one is a cache lookup, and the cost of the alternative is a
    slower page for every visitor whether or not anything is switched off.
  */
  const sections = settings.homeSections;

  // A taste of the FAQ, not the FAQ. Five is enough to answer the questions a
  // first-time buyer actually stalls on — «چقدر طول می‌کشد» and «اگر پس بدهم
  // چه» — without turning the bottom of the home page into a policy document
  // that has its own page already.
  const homeFaqs = faqs.slice(0, 5);

  const hero = {
    title: banner?.title || HERO_TITLE,
    subtitle: banner?.subtitle || HERO_SUBTITLE,
    image: banner?.imageUrl || HERO_IMAGE,
  };

  return (
    /*
      The scanning rail rather than the reading one: this page is shelves —
      a hero, category tiles and two product rails — and none of it is
      paragraphs. See `Container`.
    */
    <Container wide className="flex flex-col gap-xl py-lg md:py-xl">
      {/*
        Hero — 400px on mobile, 600px on desktop, per the two design
        variants, but `min-h` rather than `h`: a fixed height clipped the
        overlay card's own content (title + intro + CTA) whenever it grew
        past that height on a narrow screen, cutting the button off with no
        visible sign anything was missing.
      */}
      <section className="paper-card relative flex min-h-[400px] items-end overflow-hidden rounded-xl p-lg shadow-soft md:min-h-[600px] md:items-center md:justify-center md:p-xl md:text-center xl:min-h-[680px]">
        {/*
          The hero is the largest thing on the page and so almost always the
          LCP element. As a CSS `background-image` it could not be found until
          the stylesheet had parsed, and it arrived at full size in its original
          format. `next/image` emits a preload for it, serves AVIF/WebP, and
          picks a width from `sizes` — the same picture, a fraction of the bytes,
          requested at the start of the page load rather than the middle.
        */}
        <Image
          src={hero.image}
          alt=""
          fill
          priority
          sizes="100vw"
          className="object-cover opacity-80 mix-blend-multiply"
        />

        <div className="relative z-10 w-full rounded-lg bg-surface/90 p-md backdrop-blur-md md:flex md:max-w-2xl md:flex-col md:items-center md:gap-gutter md:rounded-xl md:border md:border-outline-variant/20 md:bg-surface-container-lowest/80 md:p-xl xl:max-w-3xl">
          {/*
            One size step, at `xl` — the same width where the rails below go
            five to a view. A tablet keeps the hero it had: the step is about
            the room a wide desktop has spare, not about the heading being
            wrong at 768px.
          */}
          <h1 className="mb-sm font-headline text-headline-xl text-primary-container xl:text-hero">
            {hero.title}
          </h1>
          <p className="mb-lg text-body-md leading-relaxed text-on-surface-variant md:mb-0 md:max-w-xl md:text-body-lg">
            {hero.subtitle}
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

        <div className="grid grid-cols-2 gap-gutter md:grid-cols-4 xl:grid-cols-5">
          {categories.slice(0, 5).map((category, index) => (
            <Link
              key={category.slug}
              href={routes.category(category.slug)}
              className={cn(
                'paper-card group flex flex-col items-center gap-sm rounded-lg p-md text-center transition-shadow hover:shadow-soft',
                /*
                  The fifth tile appears only where there is a fifth column for
                  it. Below `xl` the row is four wide, and a fifth would drop to
                  a line of its own with three empty cells beside it — worse
                  than not offering it, on exactly the widths where the space is
                  tightest.
                */
                index === 4 && 'hidden xl:flex',
              )}
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

      {/*
        The three sections below are each dropped when they have nothing to
        show, rather than rendered as a heading over an empty state. On an
        interior page an empty state is informative — the reader went looking
        for magazine articles and deserves to be told there are none. On the
        home page nobody asked, and «مقاله‌ای نیست» on the shop's front door is
        an apology for a section the visitor never requested.
      */}

      {sections.testimonials && testimonials.length > 0 && (
        <section className="flex flex-col gap-lg">
          <SectionHeader
            title="نظرات مشتریان"
            subtitle="آنچه خریداران بوژان درباره محصول‌هایشان نوشته‌اند"
          />
          <HomeTestimonials testimonials={testimonials} />
        </section>
      )}

      {sections.articles && articles.length > 0 && (
        <section className="flex flex-col gap-lg">
          <SectionHeader
            title="مطالب وبلاگ"
            subtitle="راهنماها و ایده‌هایی برای انتخاب بهتر"
            actionLabel="مشاهده مجله"
            actionHref={routes.magazine}
          />
          <div className="grid gap-gutter md:grid-cols-2 lg:grid-cols-3">
            {articles.map((article) => (
              <ArticleCard key={article.slug} article={article} />
            ))}
          </div>
        </section>
      )}

      {sections.faq && homeFaqs.length > 0 && (
        <section className="flex flex-col gap-lg">
          {/*
            No FAQPage schema here. `/faq` already emits it over the full list,
            and a second FAQPage on the site's most-linked URL describing a
            five-question subset is the same questions claimed twice from two
            addresses — which is how a shop gets the markup ignored on both.
          */}
          <SectionHeader
            title="سوالات متداول"
            subtitle="پاسخ پرتکرارترین پرسش‌ها درباره سفارش، ارسال و مرجوعی"
            actionLabel="همه سوالات"
            actionHref={routes.faq}
          />
          <HomeFaq items={homeFaqs} />
        </section>
      )}
    </Container>
  );
}
