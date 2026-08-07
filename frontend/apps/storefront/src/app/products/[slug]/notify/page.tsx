import type { Metadata } from 'next';
import Image from 'next/image';
import { notFound } from 'next/navigation';
import { Badge, Card, Price } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { StockAlertForm } from '@/components/product/StockAlertForm';
import { getProduct } from '@/lib/api/catalog';
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
  title: 'اطلاع‌رسانی موجودی کالا',
  robots: { index: false },
};

/** Screen 87 — Back-in-stock notification. */
export default async function StockAlertPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const product = await getProduct(slug);
  if (!product) notFound();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="اطلاع‌رسانی موجودی کالا"
        backHref={routes.product(slug)}
        subtitle="به محض شارژ مجدد این محصول، از طریق روش انتخابی شما اطلاع می‌دهیم."
      />

      <Card className="flex items-center gap-md p-lg">
        <span className="relative h-20 w-20 shrink-0 overflow-hidden rounded-lg border border-outline-variant">
          <Image src={product.image} alt={product.title} fill sizes="80px" className="object-cover" />
        </span>
        <div className="flex min-w-0 flex-col gap-xs">
          <h2 className="line-clamp-2 text-body-lg font-semibold text-primary">{product.title}</h2>
          <span className="text-caption text-on-surface-variant">{product.brand}</span>
          <span className="flex flex-wrap items-center gap-sm">
            <Price value={product.price} size="sm" />
            <Badge tone="neutral">ناموجود</Badge>
          </span>
        </div>
      </Card>

      <StockAlertForm productSlug={product.slug} productTitle={product.title} />
    </Container>
  );
}
