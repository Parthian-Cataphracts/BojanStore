import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Badge, Icon, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { getCollections } from '@/lib/api/editorial';
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
  title: 'کالکشن‌های منتخب',
  description:
    'انتخاب‌هایی آماده برای شروع مدرسه، میز کار، هدیه دادن و لحظه‌های خلاق روزمره در فروشگاه بوژان.',
};

/** Screen 21 — Curated collections. */
export default async function CollectionsPage() {
  const collections = await getCollections();
  const featured = collections.find((collection) => collection.featured) ?? collections[0];
  const rest = collections.filter((collection) => collection.slug !== featured?.slug);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <header className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
          کالکشن‌های منتخب
        </h1>
        <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant">
          انتخاب‌هایی آماده برای شروع مدرسه، میز کار، هدیه دادن و لحظه‌های خلاق روزمره.
        </p>
      </header>

      {featured && (
        <section>
          {/*
            `min-h` + a flow text block, not `absolute bottom-0` inside a
            fixed height: badge + title + summary + CTA is enough content
            that a long title clipped against `overflow-hidden` with no room
            left to grow. Same fix as `CampaignLanding`.
          */}
          <Link
            href={routes.collection(featured.slug)}
            className="group relative flex min-h-[420px] items-end overflow-hidden rounded-xl shadow-soft md:min-h-[480px]"
          >
            <Image
              src={featured.cover}
              alt={featured.title}
              fill
              priority
              sizes="100vw"
              className="object-cover transition-transform duration-500 group-hover:scale-105"
            />
            <span className="absolute inset-0 bg-gradient-to-t from-inverse-surface/80 via-inverse-surface/20 to-transparent" />

            <div className="relative flex flex-col gap-md p-lg md:p-xl">
              <Badge tone="coral" className="self-start">
                پیشنهاد ویژه
              </Badge>
              <h2 className="font-headline text-headline-lg-mobile text-inverse-on-surface md:text-headline-lg">
                {featured.title}
              </h2>
              <p className="max-w-xl text-body-md leading-relaxed text-inverse-on-surface/90">
                {featured.summary}
              </p>
              <span
                className={buttonClasses({ className: 'mt-sm gap-sm self-start md:px-xl' })}
              >
                مشاهده کالکشن
                <Icon name="arrow_back" size={20} />
              </span>
            </div>
          </Link>
        </section>
      )}

      <section className="grid gap-gutter md:grid-cols-2 lg:grid-cols-3">
        {rest.map((collection) => (
          <Link
            key={collection.slug}
            href={routes.collection(collection.slug)}
            className="group relative flex min-h-[240px] items-end overflow-hidden rounded-xl shadow-ambient transition-shadow hover:shadow-soft"
          >
            <Image
              src={collection.cover}
              alt={collection.title}
              fill
              sizes="(max-width: 768px) 100vw, 33vw"
              className="object-cover transition-transform duration-500 group-hover:scale-105"
            />
            <span className="absolute inset-0 bg-gradient-to-t from-inverse-surface/85 to-transparent" />

            <div className="relative flex flex-col gap-xs p-lg">
              <h2 className="font-headline text-display-md text-inverse-on-surface">
                {collection.title}
              </h2>
              <span className="flex items-center gap-xs text-label-md font-label-md text-inverse-on-surface/90">
                مشاهده کالکشن
                <Icon name="arrow_back" size={18} />
              </span>
            </div>
          </Link>
        ))}
      </section>
    </Container>
  );
}
