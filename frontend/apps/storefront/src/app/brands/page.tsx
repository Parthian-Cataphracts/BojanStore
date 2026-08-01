import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Card, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { getBrandProfiles } from '@/lib/api/editorial';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'برندها',
  description: 'برندهای موجود در فروشگاه بوژان؛ از بوژان استودیو تا فابر کاستل و فابریانو.',
};

/** Group brands by their first letter for the alphabetical directory. */
function groupByLetter(names: { slug: string; name: string; tagline?: string }[]) {
  const groups = new Map<string, typeof names>();
  for (const brand of [...names].sort((a, b) => a.name.localeCompare(b.name, 'fa'))) {
    const letter = brand.name.charAt(0);
    groups.set(letter, [...(groups.get(letter) ?? []), brand]);
  }
  return [...groups.entries()];
}

/** Screen 26 — Brand directory. */
export default async function BrandsPage() {
  const brands = await getBrandProfiles();
  const featured = brands.filter((brand) => brand.featured);
  const groups = groupByLetter(brands);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <header className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
          برندها
        </h1>
        <p className="text-body-md text-on-surface-variant">
          {toPersianDigits(brands.length)} برند در فروشگاه بوژان
        </p>
      </header>

      <section className="flex flex-col gap-lg">
        <h2 className="font-headline text-display-md text-primary">برندهای ویژه</h2>

        <div className="grid gap-gutter sm:grid-cols-2 lg:grid-cols-4">
          {featured.map((brand) => (
            <Card key={brand.slug} className="flex flex-col overflow-hidden">
              <Link
                href={routes.brand(brand.slug)}
                className="relative block aspect-[4/3] w-full border-b border-paper-border bg-surface-container-highest"
              >
                {brand.cover && (
                  <Image
                    src={brand.cover}
                    alt={brand.name}
                    fill
                    sizes="(max-width: 640px) 100vw, 25vw"
                    className="object-cover"
                  />
                )}
              </Link>

              <div className="flex flex-1 flex-col gap-xs p-md">
                <h3 className="text-label-md font-label-md text-primary">
                  <Link href={routes.brand(brand.slug)} className="hover:text-secondary">
                    {brand.name}
                  </Link>
                </h3>
                {brand.tagline && (
                  <p className="text-caption text-on-surface-variant">{brand.tagline}</p>
                )}

                <Link
                  href={`${routes.products}?brand=${brand.slug}`}
                  className={buttonClasses({
                    variant: 'outline',
                    size: 'sm',
                    className: 'mt-md w-full',
                  })}
                >
                  مشاهده محصولات
                </Link>
              </div>
            </Card>
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-lg">
        <h2 className="font-headline text-display-md text-primary">همه برندها</h2>

        {groups.map(([letter, items]) => (
          <div key={letter} className="flex flex-col gap-sm">
            <h3 className="text-label-md font-label-md text-secondary">{letter}</h3>

            <Card className="divide-y divide-paper-border">
              {items.map((brand) => (
                <Link
                  key={brand.slug}
                  href={routes.brand(brand.slug)}
                  className="flex items-center justify-between gap-md p-md transition-colors hover:bg-surface-container-low"
                >
                  <span className="flex min-w-0 flex-col gap-xs">
                    <span className="text-body-md text-on-surface">{brand.name}</span>
                    {brand.tagline && (
                      <span className="text-caption text-on-surface-variant">{brand.tagline}</span>
                    )}
                  </span>
                  <Icon name="chevron_left" size={20} className="shrink-0 text-outline" />
                </Link>
              ))}
            </Card>
          </div>
        ))}
      </section>
    </Container>
  );
}
