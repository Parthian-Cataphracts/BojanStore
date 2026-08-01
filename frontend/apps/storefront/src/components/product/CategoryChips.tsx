import Link from 'next/link';
import { cn } from '@bojan/ui';
import type { Category } from '@/lib/api/types';
import { routes } from '@/lib/routes';

/** The horizontal category rail from screen 04. */
export function CategoryChips({
  categories,
  activeSlug,
}: {
  categories: Category[];
  activeSlug?: string;
}) {
  const chip = (active: boolean) =>
    cn(
      'whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors',
      active
        ? 'bg-primary-fixed text-on-primary-fixed'
        : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
    );

  return (
    <nav
      aria-label="دسته‌بندی‌ها"
      className="hide-scrollbar -mx-margin-mobile flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
    >
      <Link href={routes.products} className={chip(!activeSlug)}>
        همه
      </Link>
      {categories.map((category) => (
        <Link
          key={category.slug}
          href={`${routes.products}?category=${category.slug}`}
          className={chip(activeSlug === category.slug)}
        >
          {category.name}
        </Link>
      ))}
    </nav>
  );
}
