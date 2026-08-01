import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Breadcrumb, Card, Icon, SectionHeader, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { ProductGrid } from '@/components/product/ProductGrid';
import { getCollection, getCollectionProducts, getCollections } from '@/lib/api/editorial';
import { routes } from '@/lib/routes';

export async function generateStaticParams() {
  const collections = await getCollections();
  return collections.map((collection) => ({ slug: collection.slug }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const collection = await getCollection(slug);
  if (!collection) return { title: 'کالکشن' };

  return {
    title: collection.title,
    description: collection.summary,
    openGraph: { title: collection.title, images: [{ url: collection.cover }] },
  };
}

/** Screen 22 — Collection detail. */
export default async function CollectionDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const collection = await getCollection(slug);
  if (!collection) notFound();

  const [products, all] = await Promise.all([
    getCollectionProducts(collection),
    getCollections(),
  ]);
  const related = all.filter((item) => item.slug !== collection.slug).slice(0, 3);

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <Breadcrumb
        items={[
          { label: 'خانه', href: routes.home },
          { label: 'کالکشن‌ها', href: routes.collections },
          { label: collection.title },
        ]}
      />

      {/* Hero */}
      <section className="relative h-[300px] overflow-hidden rounded-xl shadow-soft md:h-[380px]">
        <Image
          src={collection.cover}
          alt={collection.title}
          fill
          priority
          sizes="100vw"
          className="object-cover"
        />
        <span className="absolute inset-0 bg-gradient-to-t from-inverse-surface/85 to-transparent" />

        <div className="absolute inset-x-0 bottom-0 flex flex-col gap-sm p-lg md:p-xl">
          <h1 className="font-headline text-headline-lg-mobile text-inverse-on-surface md:text-headline-lg">
            {collection.title}
          </h1>
          <p className="max-w-2xl text-body-md leading-relaxed text-inverse-on-surface/90">
            {collection.summary}
          </p>
        </div>
      </section>

      <section className="flex flex-col gap-lg">
        <SectionHeader title="انتخاب‌های پیشنهادی" />
        <ProductGrid products={products} />

        <Link
          href={routes.products}
          className={buttonClasses({ variant: 'outline', className: 'self-center px-xl' })}
        >
          مشاهده همه محصولات
        </Link>
      </section>

      <section className="grid gap-lg md:grid-cols-[2fr_1fr]">
        {collection.editorialNote && (
          <Card className="flex flex-col gap-md p-lg">
            <Icon name="format_quote" size={32} className="text-primary-fixed-dim" />
            <h2 className="font-headline text-display-md text-primary">
              برای یک {collection.title} بهتر
            </h2>
            <p className="text-body-md leading-loose text-on-surface-variant">
              {collection.editorialNote}
            </p>
          </Card>
        )}

        <Card className="flex flex-col gap-md p-lg">
          <h2 className="font-headline text-display-md text-primary">کالکشن‌های مرتبط</h2>
          <ul className="flex flex-col">
            {related.map((item) => (
              <li key={item.slug} className="border-b border-paper-border last:border-0">
                <Link
                  href={routes.collection(item.slug)}
                  className="flex items-center gap-sm py-md text-body-md text-on-surface transition-colors hover:text-primary"
                >
                  <Icon name="arrow_back" size={18} className="text-outline" />
                  {item.title}
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      </section>
    </Container>
  );
}
