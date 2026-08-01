import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Badge, Card, Icon, Rating, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getProduct, getProductReviews, getRatingBreakdown } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const product = await getProduct(slug);
  return { title: product ? `نظرات ${product.title}` : 'نظرات محصول' };
}

/** Screen 84 — Product reviews. */
export default async function ProductReviewsPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const product = await getProduct(slug);
  if (!product) notFound();

  const [{ average, total, counts }, reviews] = await Promise.all([
    getRatingBreakdown(slug),
    getProductReviews(slug),
  ]);
  const stars = [5, 4, 3, 2, 1] as const;

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="نظرات محصول" backHref={routes.product(slug)} subtitle={product.title} />

      {/* Score summary */}
      <Card className="grid gap-lg p-lg md:grid-cols-[200px_1fr] md:items-center">
        <div className="flex flex-col items-center gap-xs">
          <span className="tabular font-headline text-hero-mobile text-primary md:text-hero">
            {toPersianDigits(average)}
          </span>
          <Rating value={average} />
          <span className="tabular text-caption text-on-surface-variant">
            از {toPersianDigits(total)} نظر کاربران
          </span>
        </div>

        <div className="flex flex-col gap-sm">
          {stars.map((star) => {
            const count = counts[star];
            const percent = total > 0 ? Math.round((count / total) * 100) : 0;
            return (
              <div key={star} className="flex items-center gap-sm">
                <span className="tabular w-4 shrink-0 text-caption text-on-surface-variant">
                  {toPersianDigits(star)}
                </span>
                <Icon name="star" filled size={14} className="shrink-0 text-secondary-container" />
                <span
                  className="h-2 flex-1 overflow-hidden rounded-full bg-surface-container-high"
                  role="img"
                  aria-label={`${toPersianDigits(percent)}٪`}
                >
                  <span
                    className="block h-full rounded-full bg-secondary-container"
                    style={{ width: `${percent}%` }}
                  />
                </span>
                <span className="tabular w-10 shrink-0 text-caption text-outline">
                  {toPersianDigits(count)}
                </span>
              </div>
            );
          })}
        </div>
      </Card>

      <Link
        href={routes.writeReview(slug)}
        className={buttonClasses({ className: 'gap-sm self-start px-xl' })}
      >
        <Icon name="edit" size={20} />
        ثبت نظر
      </Link>

      {/* Reviews */}
      <ul className="flex flex-col gap-md">
        {reviews.map((review) => (
          <li key={review.id}>
            <Card className="flex flex-col gap-sm p-lg">
              <div className="flex flex-wrap items-center justify-between gap-sm">
                <span className="flex items-center gap-sm">
                  <span className="flex h-10 w-10 items-center justify-center rounded-full bg-soft-mint text-label-md font-semibold text-primary">
                    {review.author.charAt(0)}
                  </span>
                  <span className="flex flex-col gap-xs">
                    <span className="text-body-md font-medium text-on-surface">{review.author}</span>
                    <Rating value={review.rating} size={14} />
                  </span>
                </span>

                {review.verified && <Badge tone="mint">خرید تاییدشده</Badge>}
              </div>

              <p className="text-body-md leading-loose text-on-surface-variant">{review.body}</p>

              <div className="flex flex-wrap items-center justify-between gap-md border-t border-paper-border pt-sm">
                <span className="tabular text-caption text-outline">
                  {formatDate(review.createdAt, 'long')}
                </span>
                <span className="flex items-center gap-xs text-caption text-on-surface-variant">
                  <Icon name="thumb_up" size={16} />
                  <span className="tabular">{toPersianDigits(review.helpfulCount)} نفر مفید دانستند</span>
                </span>
              </div>
            </Card>
          </li>
        ))}
      </ul>
    </Container>
  );
}
