import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import {
  Badge,
  Card,
  Icon,
  Price,
  SectionHeader,
  buttonClasses,
  formatDate,
  serializeJsonLd,
  toPersianDigits,
} from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ArticleCard } from '@/components/magazine/ArticleCard';
import { getProduct } from '@/lib/api/catalog';
import { getArticle, getArticles, getRelatedArticles } from '@/lib/api/editorial';
import { routes } from '@/lib/routes';
import { staticParams } from '@/lib/static-params';

export async function generateStaticParams() {
  return staticParams('articles', async () =>
    (await getArticles()).map((article) => ({ slug: article.slug })),
  );
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const article = await getArticle(slug);
  if (!article) return { title: 'مقاله' };

  return {
    title: article.title,
    description: article.excerpt,
    openGraph: {
      type: 'article',
      title: article.title,
      description: article.excerpt,
      publishedTime: article.publishedAt,
      images: [{ url: article.cover }],
    },
  };
}

/** Screen 29 — Article detail. */
export default async function ArticlePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const article = await getArticle(slug);
  if (!article) notFound();

  const [related, product] = await Promise.all([
    getRelatedArticles(slug, 3),
    article.recommendedProductSlug ? getProduct(article.recommendedProductSlug) : null,
  ]);

  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: article.title,
    description: article.excerpt,
    image: article.cover,
    datePublished: article.publishedAt,
    articleSection: article.category,
    publisher: { '@type': 'Organization', name: 'بوژان' },
  };

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: serializeJsonLd(jsonLd) }}
      />

      <article className="mx-auto flex w-full max-w-3xl flex-col gap-lg">
        <figure className="paper-card relative aspect-[16/9] overflow-hidden rounded-xl shadow-soft">
          <Image
            src={article.cover}
            alt={article.title}
            fill
            priority
            sizes="(max-width: 768px) 100vw, 768px"
            className="object-cover"
          />
        </figure>

        <div className="flex flex-wrap items-center gap-md">
          <Badge tone="mint">{article.category}</Badge>
          <span className="flex items-center gap-xs text-caption text-on-surface-variant">
            <Icon name="schedule" size={16} />
            <span className="tabular">
              زمان مطالعه: {toPersianDigits(article.readingMinutes)} دقیقه
            </span>
          </span>
          <span className="tabular text-caption text-outline">
            {formatDate(article.publishedAt, 'long')}
          </span>
        </div>

        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
          {article.title}
        </h1>

        {article.body ? (
          <div className="flex flex-col gap-lg">
            {article.body.map((block, index) => {
              if (block.type === 'heading') {
                return (
                  <h2
                    key={index}
                    className="font-headline text-display-md text-primary md:text-headline-lg-mobile"
                  >
                    {block.text}
                  </h2>
                );
              }

              if (block.type === 'paragraph') {
                return (
                  <p key={index} className="text-body-md leading-loose text-on-surface md:text-body-lg">
                    {block.text}
                  </p>
                );
              }

              // Inline product recommendation from the design's editor's-pick card.
              if (!product) return null;
              return (
                <Card key={index} className="flex flex-col gap-md p-lg sm:flex-row sm:items-center">
                  <Link
                    href={routes.product(product.slug)}
                    className="relative aspect-square w-full shrink-0 overflow-hidden rounded-lg bg-surface-container-highest sm:w-40"
                  >
                    <Image
                      src={product.image}
                      alt={product.title}
                      fill
                      sizes="160px"
                      className="object-cover"
                    />
                  </Link>

                  <div className="flex min-w-0 flex-1 flex-col gap-sm">
                    <Badge tone="coral" className="self-start">
                      پیشنهاد سردبیر
                    </Badge>
                    <h3 className="font-headline text-display-md text-primary">
                      <Link href={routes.product(product.slug)} className="hover:text-secondary">
                        {product.title}
                      </Link>
                    </h3>
                    <Price
                      value={product.price}
                      {...(product.compareAtPrice ? { compareAt: product.compareAtPrice } : null)}
                    />
                    <Link
                      href={routes.product(product.slug)}
                      className={buttonClasses({ size: 'sm', className: 'mt-sm self-start px-lg' })}
                    >
                      مشاهده و خرید
                    </Link>
                  </div>
                </Card>
              );
            })}
          </div>
        ) : (
          <p className="text-body-md leading-loose text-on-surface md:text-body-lg">
            {article.excerpt}
          </p>
        )}
      </article>

      {related.length > 0 && (
        <section className="flex flex-col gap-lg border-t border-outline-variant/40 pt-xl">
          <SectionHeader
            title="مطالب مرتبط"
            actionLabel="مشاهده همه"
            actionHref={routes.magazine}
          />
          <div className="grid gap-gutter md:grid-cols-3">
            {related.map((item) => (
              <ArticleCard key={item.slug} article={item} />
            ))}
          </div>
        </section>
      )}
    </Container>
  );
}
