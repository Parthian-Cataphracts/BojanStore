import type { ReactNode } from 'react';
import { EmptyState, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductGrid } from '@/components/product/ProductGrid';
import type { Product } from '@/lib/api/types';

/**
 * Shared shell for the themed listings — screens 23 (new arrivals),
 * 24 (bestsellers) and 25 (gift & lifestyle). Each is a titled grid with an
 * editorial intro rather than the filterable catalogue of screen 04.
 */
export function ThemedListing({
  title,
  intro,
  products,
  children,
}: {
  title: string;
  intro: string;
  products: Product[];
  children?: ReactNode;
}) {
  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <header className="flex flex-col gap-sm">
        <div className="flex flex-wrap items-baseline justify-between gap-md">
          <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
            {title}
          </h1>
          <span className="tabular shrink-0 text-caption text-outline">
            {toPersianDigits(products.length)} محصول
          </span>
        </div>
        <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant">{intro}</p>
      </header>

      {children}

      {products.length > 0 ? (
        <ProductGrid products={products} />
      ) : (
        <EmptyState
          icon="inventory_2"
          title="فعلاً محصولی اینجا نیست"
          description="به‌زودی محصولات این بخش اضافه می‌شوند."
        />
      )}
    </Container>
  );
}
