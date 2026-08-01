import Link from 'next/link';
import { Icon, buttonClasses } from '@bojan/ui';
import type { Category } from '@/lib/api/types';
import { routes } from '@/lib/routes';

/**
 * Screen 37 — Empty result.
 *
 * More than a bare empty state: the design offers a way out (clear filters,
 * browse categories) plus suggested categories, so a dead end still leads
 * somewhere.
 */
export function NoResults({
  title = 'چیزی پیدا نشد',
  description = 'نتیجه‌ای برای انتخاب فعلی شما پیدا نکردیم. می‌توانید فیلترها را تغییر دهید یا دسته‌بندی‌های دیگر را ببینید.',
  clearHref,
  categories,
}: {
  title?: string;
  description?: string;
  /** Where "clear filters" goes; omitted when there are no filters to clear. */
  clearHref?: string;
  categories: Category[];
}) {
  return (
    <div className="flex flex-col items-center gap-lg py-xl text-center">
      <span className="flex h-24 w-24 items-center justify-center rounded-full bg-surface-container-high text-outline">
        <Icon name="search_off" size={44} />
      </span>

      <div className="flex flex-col gap-sm">
        <h2 className="font-headline text-display-md text-primary">{title}</h2>
        <p className="mx-auto max-w-md text-body-md leading-loose text-on-surface-variant">
          {description}
        </p>
      </div>

      <div className="flex w-full max-w-md flex-col gap-md sm:flex-row sm:justify-center">
        {clearHref && (
          <Link href={clearHref} className={buttonClasses({ fullWidth: true })}>
            حذف فیلترها
          </Link>
        )}
        <Link
          href={routes.categories}
          className={buttonClasses({ variant: 'outline', fullWidth: true })}
        >
          مشاهده دسته‌بندی‌ها
        </Link>
      </div>

      {categories.length > 0 && (
        <section className="flex flex-col items-center gap-md pt-md">
          <h3 className="text-label-md font-label-md text-primary">پیشنهادهای ما برای شما</h3>
          <div className="flex flex-wrap justify-center gap-sm">
            {categories.map((category) => (
              <Link
                key={category.slug}
                href={routes.category(category.slug)}
                className="rounded-full bg-soft-mint px-md py-sm text-label-md font-label-md text-primary transition-opacity hover:opacity-80"
              >
                {category.name}
              </Link>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
