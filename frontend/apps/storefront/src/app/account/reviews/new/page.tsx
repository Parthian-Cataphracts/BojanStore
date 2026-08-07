import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ReviewForm } from '@/components/account/ReviewForm';
import { getProduct } from '@/lib/api/catalog';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

/*
 * Rendered on request, not at build.
 *
 * This page reads the catalogue, and the catalogue lives behind the API — which
 * does not exist when the image is built. Prerendering it meant `next build`
 * fetching from a host that is not up yet, which is exactly how the Docker
 * build failed. The alternative, emitting it with whatever an unreachable API
 * returns, is worse: the first visitors after a deploy would be served an empty
 * shop until the first revalidation filled it in.
 *
 * Nothing is lost by it. The fetches underneath already declare their own
 * `revalidate` window, so the API is not called per request either way — the
 * caching just happens a layer down, where stock and prices can expire on their
 * own schedule instead of being frozen into the image.
 */
export const dynamic = 'force-dynamic';

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
