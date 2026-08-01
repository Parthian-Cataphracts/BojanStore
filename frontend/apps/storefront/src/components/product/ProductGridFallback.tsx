import { ProductCardSkeleton } from '@bojan/ui';

export function ProductGridFallback({ count = 8 }: { count?: number }) {
  return (
    <div className="grid grid-cols-2 gap-gutter md:grid-cols-3 lg:grid-cols-4">
      {Array.from({ length: count }, (_, index) => (
        <ProductCardSkeleton key={index} />
      ))}
    </div>
  );
}
