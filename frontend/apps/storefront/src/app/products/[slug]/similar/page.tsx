import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { EmptyState } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ProductGrid } from '@/components/product/ProductGrid';
import { getProduct, getRelatedProducts } from '@/lib/api/catalog';
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

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const product = await getProduct(slug);
  return { title: product ? `مشابه ${product.title}` : 'محصولات مشابه' };
}

/** Screen 88 — Similar products. */
export default async function SimilarProductsPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const [product, similar] = await Promise.all([getProduct(slug), getRelatedProducts(slug, 24)]);
  if (!product) notFound();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="محصولات مشابه"
        backHref={routes.product(slug)}
        subtitle={`مشابه با: ${product.title}`}
      />

      {similar.length > 0 ? (
        <ProductGrid products={similar} />
      ) : (
        <EmptyState
          icon="inventory_2"
          title="محصول مشابهی پیدا نشد"
          description="فعلاً کالای مشابهی در این دسته‌بندی موجود نیست."
        />
      )}
    </Container>
  );
}
