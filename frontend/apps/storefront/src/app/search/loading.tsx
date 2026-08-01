import { Skeleton } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductGridFallback } from '@/components/product/ProductGridFallback';

/** Shown while a search runs — see the note on the catalogue's loading state. */
export default function Loading() {
  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <div className="flex flex-col gap-md">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-12 w-full rounded-lg" />
      </div>

      <section className="flex flex-col gap-lg">
        <div className="flex items-baseline justify-between gap-md">
          <Skeleton className="h-5 w-56" />
          <Skeleton className="h-4 w-16" />
        </div>
        <ProductGridFallback count={8} />
      </section>
    </Container>
  );
}
