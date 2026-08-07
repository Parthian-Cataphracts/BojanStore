import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { FullscreenGallery } from '@/components/product/FullscreenGallery';
import { getProduct } from '@/lib/api/catalog';

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
  title: 'گالری تصاویر محصول',
  robots: { index: false },
};

/** Screen 83 — Fullscreen product gallery. */
export default async function ProductGalleryPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const product = await getProduct(slug);
  if (!product) notFound();

  const images = product.gallery?.length ? product.gallery : [product.image];

  return (
    <FullscreenGallery
      slug={slug}
      title={product.title}
      alt={product.imageAlt || product.title}
      images={images}
    />
  );
}
