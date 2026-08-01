import type { Metadata } from 'next';
import Link from 'next/link';
import { EmptyState, SectionHeader } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ArticleCard } from '@/components/magazine/ArticleCard';
import { getArticleCategories, getArticles } from '@/lib/api/editorial';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'مجله بوژان',
  description:
    'راهنماها، ایده‌ها و پیشنهادهایی برای انتخاب بهتر نوشت‌افزار، ابزار هنری و لوازم میز کار.',
};

/** Screen 28 — Magazine. */
export default async function MagazinePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const category = first(params.category);

  // The tabs are the categories the magazine actually has. They were a fixed
  // list, so a category the panel introduced never appeared and one whose last
  // article was unpublished stayed on as a tab leading to an empty page.
  const [categories, articles] = await Promise.all([
    getArticleCategories(),
    getArticles(category),
  ]);

  const featured = !category ? articles.find((article) => article.featured) : undefined;
  const rest = featured ? articles.filter((article) => article.slug !== featured.slug) : articles;

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <header className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
          مجله بوژان
        </h1>
        <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant">
          راهنماها، ایده‌ها و پیشنهادهایی برای انتخاب بهتر.
        </p>
      </header>

      <nav
        aria-label="دسته‌بندی مقالات"
        className="hide-scrollbar -mx-margin-mobile flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
      >
        <Link
          href={routes.magazine}
          className={`shrink-0 whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors ${
            !category
              ? 'bg-primary-fixed text-on-primary-fixed'
              : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant'
          }`}
        >
          همه مقالات
        </Link>
        {categories.map((name) => (
          <Link
            key={name}
            href={`${routes.magazine}?category=${encodeURIComponent(name)}`}
            className={`shrink-0 whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors ${
              category === name
                ? 'bg-primary-fixed text-on-primary-fixed'
                : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant'
            }`}
          >
            {name}
          </Link>
        ))}
      </nav>

      {featured && (
        <section className="flex flex-col gap-lg">
          <SectionHeader title="مقاله ویژه" />
          <ArticleCard article={featured} featured />
        </section>
      )}

      {rest.length > 0 ? (
        <section className="flex flex-col gap-lg">
          <SectionHeader title={category ? `مقالات ${category}` : 'تازه‌ترین مقالات'} />
          <div className="grid gap-gutter md:grid-cols-2 lg:grid-cols-3">
            {rest.map((article) => (
              <ArticleCard key={article.slug} article={article} />
            ))}
          </div>
        </section>
      ) : (
        <EmptyState
          icon="article"
          title="مقاله‌ای در این دسته نیست"
          description="دسته‌بندی دیگری را انتخاب کنید یا همه مقالات را ببینید."
        />
      )}
    </Container>
  );
}
