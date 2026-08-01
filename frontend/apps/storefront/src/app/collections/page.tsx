import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Badge, Icon, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { getCollections } from '@/lib/api/editorial';
import { routes } from '@/lib/routes';

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
          <Link
            href={routes.collection(featured.slug)}
            className="group relative block h-[420px] overflow-hidden rounded-xl shadow-soft md:h-[480px]"
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

            <div className="absolute inset-x-0 bottom-0 flex flex-col gap-md p-lg md:p-xl">
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
            className="group relative block h-[240px] overflow-hidden rounded-xl shadow-ambient transition-shadow hover:shadow-soft"
          >
            <Image
              src={collection.cover}
              alt={collection.title}
              fill
              sizes="(max-width: 768px) 100vw, 33vw"
              className="object-cover transition-transform duration-500 group-hover:scale-105"
            />
            <span className="absolute inset-0 bg-gradient-to-t from-inverse-surface/85 to-transparent" />

            <div className="absolute inset-x-0 bottom-0 flex flex-col gap-xs p-lg">
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
