import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ReviewForm } from '@/components/account/ReviewForm';
import { getProduct } from '@/lib/api/catalog';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'ثبت نظر برای محصول',
  robots: { index: false },
};

/** Screen 56 — Write a review. */
export default async function WriteReviewPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const slug = first(params.product);
  if (!slug) notFound();

  const product = await getProduct(slug);
  if (!product) notFound();

  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="ثبت نظر برای محصول" backHref={routes.reviews} />
      <ReviewForm product={product} />
    </Container>
  );
}
