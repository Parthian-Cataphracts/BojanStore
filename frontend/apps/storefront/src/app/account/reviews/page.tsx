import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Badge, Card, EmptyState, Icon, Rating, buttonClasses, formatDate } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getAwaitingReviews, getMyReviews } from '@/lib/api/activity';
import type { ReviewStatus } from '@/lib/api/types';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'نظرات من',
  robots: { index: false },
};

const statusMeta: Record<ReviewStatus, { label: string; tone: 'mint' | 'warning' | 'error'; icon: string }> = {
  published: { label: 'منتشر شده', tone: 'mint', icon: 'check_circle' },
  pending: { label: 'در حال بررسی', tone: 'warning', icon: 'pending' },
  rejected: { label: 'تایید نشده', tone: 'error', icon: 'cancel' },
};

/** Screen 55 — My reviews. */
export default async function ReviewsPage() {
  const [reviews, awaiting] = await Promise.all([getMyReviews(), getAwaitingReviews()]);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <PageHeader title="نظرات من" backHref={routes.account} />

      {/* Purchases still waiting for a review */}
      {awaiting.length > 0 && (
        <section className="flex flex-col gap-md">
          <div className="flex flex-col gap-xs">
            <h2 className="font-headline text-display-md text-primary">خریدهای اخیر شما</h2>
            <p className="text-caption text-on-surface-variant">
              تجربه خرید خود را با دیگران به اشتراک بگذارید.
            </p>
          </div>

          {awaiting.map((item) => (
            <Card key={item.productSlug} className="flex flex-wrap items-center gap-md p-lg">
              <Link
                href={routes.product(item.productSlug)}
                className="relative h-16 w-16 shrink-0 overflow-hidden rounded-lg border border-outline-variant"
              >
                <Image
                  src={item.productImage}
                  alt={item.productTitle}
                  fill
                  sizes="64px"
                  className="object-cover"
                />
              </Link>

              <div className="flex min-w-0 flex-1 flex-col gap-xs">
                <span className="line-clamp-2 text-body-md text-on-surface">{item.productTitle}</span>
                <Badge tone="mint" className="self-start">
                  تحویل داده شده
                </Badge>
              </div>

              <Link
                href={routes.writeReview(item.productSlug)}
                className={buttonClasses({ size: 'sm', className: 'gap-xs' })}
              >
                <Icon name="edit" size={18} />
                ثبت نظر
              </Link>
            </Card>
          ))}
        </section>
      )}

      {/* Reviews already written */}
      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-display-md text-primary">نظرهای ثبت‌شده</h2>

        {reviews.length > 0 ? (
          <ul className="flex flex-col gap-md">
            {reviews.map((review) => {
              const status = statusMeta[review.status];
              return (
                <li key={review.id}>
                  <Card className="flex flex-col gap-md p-lg">
                    <div className="flex items-start gap-md">
                      <Link
                        href={routes.product(review.productSlug)}
                        className="relative h-16 w-16 shrink-0 overflow-hidden rounded-lg border border-outline-variant"
                      >
                        <Image
                          src={review.productImage}
                          alt={review.productTitle}
                          fill
                          sizes="64px"
                          className="object-cover"
                        />
                      </Link>

                      <div className="flex min-w-0 flex-1 flex-col gap-xs">
                        <h3 className="line-clamp-2 text-body-md text-on-surface">
                          <Link
                            href={routes.product(review.productSlug)}
                            className="transition-colors hover:text-primary"
                          >
                            {review.productTitle}
                          </Link>
                        </h3>
                        <Rating value={review.rating} />
                      </div>
                    </div>

                    <p className="text-body-md leading-loose text-on-surface-variant">
                      {review.body}
                    </p>

                    <div className="flex flex-wrap items-center gap-md border-t border-paper-border pt-md">
                      <Badge tone={status.tone}>
                        <Icon name={status.icon} size={16} />
                        {status.label}
                      </Badge>
                      <span className="tabular text-caption text-outline">
                        {formatDate(review.createdAt, 'long')}
                      </span>
                    </div>
                  </Card>
                </li>
              );
            })}
          </ul>
        ) : (
          <EmptyState
            icon="rate_review"
            title="هنوز نظری ثبت نکرده‌اید"
            description="پس از دریافت سفارش می‌توانید تجربه خود را با دیگران به اشتراک بگذارید."
          />
        )}
      </section>
    </Container>
  );
}
