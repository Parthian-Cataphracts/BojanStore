'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState } from 'react';
import { Button, Checkbox, Icon, Radio, Sheet } from '@bojan/ui';
import type { ProductQuery } from '@/lib/api/types';

const sortOptions: { value: NonNullable<ProductQuery['sort']>; label: string }[] = [
  { value: 'newest', label: 'جدیدترین' },
  { value: 'bestselling', label: 'پرفروش‌ترین' },
  { value: 'price-asc', label: 'ارزان‌ترین' },
  { value: 'price-desc', label: 'گران‌ترین' },
  { value: 'rating', label: 'بیشترین امتیاز' },
];

const priceBuckets = [
  { label: 'تا ۵۰۰ هزار تومان', max: 500_000 },
  { label: '۵۰۰ هزار تا ۱ میلیون', min: 500_000, max: 1_000_000 },
  { label: '۱ تا ۳ میلیون', min: 1_000_000, max: 3_000_000 },
  { label: 'بیشتر از ۳ میلیون', min: 3_000_000 },
];

export interface CatalogToolbarProps {
  /** Brand facets rendered inside the filter sheet. */
  brands: { slug: string; name: string }[];
}

/**
 * The filter / sort control row from screens 04, 81 and 82.
 * State lives in the URL so listings stay shareable and server-rendered.
 */
export function CatalogToolbar({ brands }: CatalogToolbarProps) {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [filtersOpen, setFiltersOpen] = useState(false);
  const [sortOpen, setSortOpen] = useState(false);

  const activeSort = searchParams.get('sort') ?? 'newest';
  const activeBrand = searchParams.get('brand');
  const inStockOnly = searchParams.get('inStock') === 'true';

  function apply(changes: Record<string, string | null>) {
    const params = new URLSearchParams(searchParams.toString());
    for (const [key, value] of Object.entries(changes)) {
      if (value === null) params.delete(key);
      else params.set(key, value);
    }
    // Any facet change resets pagination.
    params.delete('page');
    router.push(`?${params.toString()}`, { scroll: false });
  }

  const activeFilterCount = [activeBrand, inStockOnly ? 'stock' : null, searchParams.get('maxPrice')].filter(
    Boolean,
  ).length;

  return (
    <>
      <div className="flex items-center gap-sm">
        <button
          type="button"
          onClick={() => setFiltersOpen(true)}
          className="flex items-center gap-xs rounded-full border border-outline-variant px-sm py-xs text-label-md font-label-md text-on-surface-variant transition-colors hover:bg-surface-variant"
        >
          <Icon name="tune" size={18} />
          فیلتر
          {activeFilterCount > 0 && (
            <span className="tabular flex h-5 min-w-5 items-center justify-center rounded-full bg-secondary-container px-1 text-caption text-on-tertiary">
              {activeFilterCount}
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => setSortOpen(true)}
          className="flex items-center gap-xs rounded-full border border-outline-variant px-sm py-xs text-label-md font-label-md text-on-surface-variant transition-colors hover:bg-surface-variant"
        >
          <Icon name="swap_vert" size={18} />
          مرتب‌سازی
        </button>
      </div>

      <Sheet open={sortOpen} onClose={() => setSortOpen(false)} title="مرتب‌سازی">
        <div className="flex flex-col gap-md">
          {sortOptions.map((option) => (
            <Radio
              key={option.value}
              name="sort"
              label={option.label}
              checked={activeSort === option.value}
              onChange={() => {
                apply({ sort: option.value });
                setSortOpen(false);
              }}
            />
          ))}
        </div>
      </Sheet>

      <Sheet
        open={filtersOpen}
        onClose={() => setFiltersOpen(false)}
        title="فیلترها"
        footer={
          <div className="flex gap-md">
            <Button
              variant="outline"
              fullWidth
              onClick={() => {
                apply({ brand: null, inStock: null, minPrice: null, maxPrice: null });
                setFiltersOpen(false);
              }}
            >
              حذف فیلترها
            </Button>
            <Button fullWidth onClick={() => setFiltersOpen(false)}>
              اعمال فیلتر
            </Button>
          </div>
        }
      >
        <div className="flex flex-col gap-xl">
          <fieldset className="flex flex-col gap-md">
            <legend className="mb-sm text-label-md font-label-md text-primary">محدوده قیمت</legend>
            {priceBuckets.map((bucket) => (
              <Radio
                key={bucket.label}
                name="price"
                label={bucket.label}
                checked={String(bucket.max ?? '') === (searchParams.get('maxPrice') ?? '')}
                onChange={() =>
                  apply({
                    minPrice: bucket.min ? String(bucket.min) : null,
                    maxPrice: bucket.max ? String(bucket.max) : null,
                  })
                }
              />
            ))}
          </fieldset>

          <fieldset className="flex flex-col gap-md">
            <legend className="mb-sm text-label-md font-label-md text-primary">برند</legend>
            {brands.map((brand) => (
              <Radio
                key={brand.slug}
                name="brand"
                label={brand.name}
                checked={activeBrand === brand.slug}
                onChange={() => apply({ brand: brand.slug })}
              />
            ))}
          </fieldset>

          <Checkbox
            label="فقط کالاهای موجود"
            checked={inStockOnly}
            onChange={(event) => apply({ inStock: event.target.checked ? 'true' : null })}
          />
        </div>
      </Sheet>
    </>
  );
}
