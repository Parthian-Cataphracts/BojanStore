import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { FullscreenGallery } from '@/components/product/FullscreenGallery';
import { getProduct } from '@/lib/api/catalog';

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
