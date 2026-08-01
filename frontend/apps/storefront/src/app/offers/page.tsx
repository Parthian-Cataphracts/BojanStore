import type { Metadata } from 'next';
import { SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductGrid, ProductRail } from '@/components/product/ProductGrid';
import { getBestsellers, getProducts } from '@/lib/api/catalog';
import { getRecentlyViewed } from '@/lib/api/activity';

export const metadata: Metadata = {
  title: 'پیشنهادهای ویژه برای شما',
  description: 'مجموعه‌ای انتخاب‌شده بر اساس سلیقه، بازدیدهای اخیر و خریدهای گذشته شما.',
};

/** Screen 89 — Personalised offers. */
export default async function OffersPage() {
  const [{ items }, viewed, bestsellers] = await Promise.all([
    getProducts({ pageSize: 24 }),
    getRecentlyViewed(),
    getBestsellers(8),
  ]);

  // "Offers" are the discounted products; the rest of the page is discovery.
  const discounted = items.filter((product) => product.compareAtPrice);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <header className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-page-title">
          پیشنهادهای ویژه برای شما
        </h1>
        <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant">
          مجموعه‌ای با دقت انتخاب‌شده بر اساس سلیقه، بازدیدهای اخیر و خریدهای گذشته شما.
        </p>
      </header>

      <section className="flex flex-col gap-lg">
        <SectionHeader title="تخفیف‌های فعال" subtitle="تا زمانی که موجودی هست" />
        <ProductGrid products={discounted} />
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title="بر اساس بازدیدهای اخیر شما" />
        <ProductRail products={viewed} />
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title="محبوب بین مشتریان بوژان" />
        <ProductRail products={bestsellers} />
      </section>
    </Container>
  );
}
