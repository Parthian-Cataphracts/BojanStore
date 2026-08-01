import type { Metadata } from 'next';
import Image from 'next/image';
import { notFound } from 'next/navigation';
import { Breadcrumb, EmptyState, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductGrid } from '@/components/product/ProductGrid';
import { getProducts } from '@/lib/api/catalog';
import { getBrandProfile, getBrandProfiles } from '@/lib/api/editorial';
import { routes } from '@/lib/routes';

export async function generateStaticParams() {
  const brands = await getBrandProfiles();
  return brands.map((brand) => ({ slug: brand.slug }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const brand = await getBrandProfile(slug);
  if (!brand) return { title: 'برند' };

  return {
    title: brand.name,
    description: brand.description ?? `محصولات برند ${brand.name} در فروشگاه بوژان.`,
  };
}

/** Screen 27 — Brand detail. */
export default async function BrandDetailPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const brand = await getBrandProfile(slug);
  if (!brand) notFound();

  const { items } = await getProducts({ brand: slug, pageSize: 24 });

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <Breadcrumb
        items={[
          { label: 'خانه', href: routes.home },
          { label: 'برندها', href: routes.brands },
          { label: brand.name },
        ]}
      />

      <section className="flex flex-col gap-md">
        {brand.cover && (
          <div className="paper-card relative h-[200px] overflow-hidden rounded-xl md:h-[280px]">
            <Image
              src={brand.cover}
              alt={brand.name}
              fill
              priority
              sizes="100vw"
              className="object-cover"
            />
          </div>
        )}

        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
          {brand.name}
        </h1>

        {brand.description && (
          <p className="max-w-3xl text-body-md leading-loose text-on-surface-variant">
            {brand.description}
          </p>
        )}

        <p className="tabular text-caption text-outline">
          {toPersianDigits(brand.productCount)} محصول
        </p>
      </section>

      {items.length > 0 ? (
        <ProductGrid products={items} />
      ) : (
        <EmptyState
          icon="inventory_2"
          title={`فعلاً محصولی از ${brand.name} موجود نیست`}
          description="به‌زودی محصولات این برند اضافه می‌شوند."
        />
      )}
    </Container>
  );
}
