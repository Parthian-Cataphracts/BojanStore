'use client';

import Link from 'next/link';
import { EmptyState, ProductCardSkeleton, buttonClasses } from '@bojan/ui';
import { ProductGrid } from '@/components/product/ProductGrid';
import { useBrowsing } from '@/lib/browsing/store';
import { routes } from '@/lib/routes';

/**
 * Screen 57's grid.
 *
 * Reads the browsing store rather than a fixture, so it lists the products this
 * shopper actually opened — most recent first. Clearing it here is real, not a
 * local filter that comes back on the next visit.
 */
export function RecentlyViewedGrid() {
  const { viewed, hydrated, clearViewed } = useBrowsing();

  // Storage is read after mount; the empty state would otherwise flash at
  // someone who does have history.
  if (!hydrated) {
    return (
      <div className="grid grid-cols-2 gap-md md:grid-cols-4">
        <ProductCardSkeleton />
        <ProductCardSkeleton />
        <ProductCardSkeleton />
        <ProductCardSkeleton />
      </div>
    );
  }

  if (viewed.length === 0) {
    return (
      <EmptyState
        icon="history"
        title="هنوز محصولی ندیده‌اید"
        description="محصولاتی که مشاهده کنید برای دسترسی سریع‌تر اینجا ذخیره می‌شوند."
        action={
          <Link href={routes.products} className={buttonClasses()}>
            مشاهده محصولات
          </Link>
        }
      />
    );
  }

  return (
    <div className="flex flex-col gap-lg">
      <ProductGrid products={viewed} />

      <button
        type="button"
        onClick={clearViewed}
        className="self-start text-label-md font-medium text-secondary transition-colors hover:text-primary"
      >
        پاک کردن تاریخچه
      </button>
    </div>
  );
}
