import { ProductCard } from './ProductCard';
import type { Product } from '@/lib/api/types';

/** 2-up on mobile, 3-up on tablet, 4-up on desktop — as drawn in screen 04. */
export function ProductGrid({ products }: { products: Product[] }) {
  return (
    <div className="grid grid-cols-2 gap-gutter md:grid-cols-3 lg:grid-cols-4">
      {products.map((product, index) => (
        <ProductCard key={product.id} product={product} priority={index < 4} />
      ))}
    </div>
  );
}

/** Horizontally scrolling rail used by the homepage sections. */
export function ProductRail({ products }: { products: Product[] }) {
  return (
    <div className="hide-scrollbar -mx-margin-mobile flex gap-gutter overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0">
      {products.map((product) => (
        <ProductCard key={product.id} product={product} railWidth />
      ))}
    </div>
  );
}
