import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { EmptyState } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { ProductGrid } from '@/components/product/ProductGrid';
import { getProduct, getRelatedProducts } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

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
