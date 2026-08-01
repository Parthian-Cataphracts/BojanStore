import Image from 'next/image';
import Link from 'next/link';
import { Card, Icon, SectionHeader, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductGrid, ProductRail } from '@/components/product/ProductGrid';
import type { Product } from '@/lib/api/types';

export interface CampaignShortcut {
  label: string;
  icon: string;
  href: string;
}

/**
 * Shared layout for the campaign landings (screens 48 and 49): full-bleed hero,
 * shortcut chips, a featured grid and a discovery rail.
 */
export function CampaignLanding({
  title,
  intro,
  cover,
  ctaLabel,
  ctaHref,
  shortcuts,
  shortcutsTitle,
  featured,
  featuredTitle,
  rail,
  railTitle,
}: {
  title: string;
  intro: string;
  cover: string;
  ctaLabel: string;
  ctaHref: string;
  shortcuts: CampaignShortcut[];
  shortcutsTitle: string;
  featured: Product[];
  featuredTitle: string;
  rail: Product[];
  railTitle: string;
}) {
  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <section className="relative flex h-[380px] items-end overflow-hidden rounded-xl shadow-soft md:h-[460px]">
        <Image src={cover} alt={title} fill priority sizes="100vw" className="object-cover" />
        <span className="absolute inset-0 bg-gradient-to-t from-inverse-surface/85 via-inverse-surface/30 to-transparent" />

        <div className="relative flex flex-col gap-md p-lg md:p-xl">
          <h1 className="font-headline text-hero-mobile text-inverse-on-surface md:text-hero">
            {title}
          </h1>
          <p className="max-w-2xl text-body-md leading-loose text-inverse-on-surface/90 md:text-body-lg">
            {intro}
          </p>
          <Link href={ctaHref} className={buttonClasses({ size: 'lg', className: 'self-start px-xl' })}>
            {ctaLabel}
          </Link>
        </div>
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title={shortcutsTitle} />
        <div className="grid grid-cols-2 gap-gutter md:grid-cols-4">
          {shortcuts.map((shortcut) => (
            <Link key={shortcut.href} href={shortcut.href} className="group block">
              <Card className="flex h-full flex-col items-center gap-sm p-lg text-center transition-shadow group-hover:shadow-soft">
                <span className="flex h-14 w-14 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container transition-transform group-hover:scale-110">
                  <Icon name={shortcut.icon} size={26} />
                </span>
                <span className="text-label-md font-medium text-primary-container">
                  {shortcut.label}
                </span>
              </Card>
            </Link>
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title={featuredTitle} />
        <ProductGrid products={featured} />
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title={railTitle} />
        <ProductRail products={rail} />
      </section>
    </Container>
  );
}
