import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import {
  Badge,
  Breadcrumb,
  Icon,
  Price,
  Rating,
  SectionHeader,
  serializeJsonLd,
  toPersianDigits,
} from '@bojan/ui';
import Link from 'next/link';
import { buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { RecordProductView } from '@/components/product/RecordProductView';
import { ProductPurchase } from '@/components/product/ProductPurchase';
import { ProductGallery } from '@/components/product/ProductGallery';
import { ProductRail } from '@/components/product/ProductGrid';
import {
  getProduct,
  getProducts,
  getProductSkus,
  getRelatedProducts,
  getVariantAxes,
} from '@/lib/api/catalog';
import { routes } from '@/lib/routes';
import { absoluteUrl, toRial } from '@/lib/seo';

export async function generateStaticParams() {
  const { items } = await getProducts({ pageSize: 100 });
  return items.map((product) => ({ slug: product.slug }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const product = await getProduct(slug);
  if (!product) return { title: 'محصول یافت نشد' };

  return {
    title: product.title,
    description: product.description ?? `خرید ${product.title} از برند ${product.brand} در بوژان.`,
    alternates: { canonical: routes.product(product.slug) },
    openGraph: {
      title: product.title,
      images: [{ url: product.image }],
    },
  };
}

/** Screen 06 — Product detail. */
export default async function ProductPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;

  const [product, related, variantAxes, skus] = await Promise.all([
    getProduct(slug),
    getRelatedProducts(slug, 8),
    getVariantAxes(slug),
    getProductSkus(slug),
  ]);
  if (!product) notFound();

  const images = product.gallery?.length ? product.gallery : [product.image];
  const specs = product.specs ?? [
    { label: 'برند', value: product.brand },
    { label: 'دسته‌بندی', value: product.categoryName },
    { label: 'کد کالا', value: toPersianDigits(product.id.replace(/\D/g, '')) },
  ];

  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: product.title,
    description: product.description ?? undefined,
    image: images,
    sku: product.id,
    brand: { '@type': 'Brand', name: product.brand },
    ...(product.reviewCount > 0
      ? {
          aggregateRating: {
            '@type': 'AggregateRating',
            ratingValue: product.rating,
            reviewCount: product.reviewCount,
          },
        }
      : null),
    offers: {
      '@type': 'Offer',
      // ISO 4217 has no code for Toman, so the amount is converted to Rial
      // rather than the currency being relabelled. Declaring the Toman figure
      // as IRR advertised every product at a tenth of its price.
      priceCurrency: 'IRR',
      price: toRial(product.price),
      availability:
        product.stock > 0 ? 'https://schema.org/InStock' : 'https://schema.org/OutOfStock',
      // Absolute: schema.org URL values are not resolved against `metadataBase`
      // the way Next's own metadata is.
      url: absoluteUrl(routes.product(product.slug)),
    },
  };

  return (
    <Container className="flex flex-col gap-xl py-lg pb-[168px] md:pb-xl md:py-xl">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: serializeJsonLd(jsonLd) }}
      />
      <RecordProductView product={product} />
      <Breadcrumb
        items={[
          { label: 'خانه', href: routes.home },
          { label: product.categoryName, href: routes.category(product.categorySlug) },
          { label: product.title },
        ]}
      />

      <div className="grid gap-xl md:grid-cols-2">
        <ProductGallery images={images} alt={product.imageAlt || product.title} />

        <div className="flex flex-col gap-lg">
          <header className="flex flex-col gap-sm">
            <span className="text-caption text-outline">{product.brand}</span>
            <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
              {product.title}
            </h1>

            <div className="flex flex-wrap items-center gap-md">
              <Rating value={product.rating} count={product.reviewCount} />
              {product.isBestseller && <Badge tone="mint">پرفروش</Badge>}
              {product.isNew && <Badge tone="coral">جدید</Badge>}
            </div>

            <Price
              value={product.price}
              {...(product.compareAtPrice ? { compareAt: product.compareAtPrice } : null)}
              size="lg"
              className="mt-sm"
            />

            <p
              className={`flex items-center gap-xs text-caption ${
                product.stock > 0 ? 'text-tertiary' : 'text-error'
              }`}
            >
              <Icon name={product.stock > 0 ? 'check_circle' : 'cancel'} size={16} />
              {product.stock > 0
                ? `موجود در انبار (${toPersianDigits(product.stock)} عدد)`
                : 'در حال حاضر ناموجود است'}
            </p>
          </header>

          {/* Add to cart — inline here on desktop, sticky on mobile. */}
          {product.stock > 0 ? (
            <ProductPurchase product={product} variantAxes={variantAxes} skus={skus} />
          ) : (
            <Link
              href={routes.productNotify(product.slug)}
              className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm' })}
            >
              <Icon name="notifications_active" size={20} />
              به من اطلاع بده
            </Link>
          )}

          {/* Deep links to the detail sub-screens. */}
          <nav className="grid grid-cols-2 gap-sm">
            {[
              { label: 'گالری تصاویر', icon: 'photo_library', href: routes.productGallery(product.slug) },
              { label: 'نظرات کاربران', icon: 'reviews', href: routes.productReviews(product.slug) },
              { label: 'پرسش و پاسخ', icon: 'help', href: routes.productQuestions(product.slug) },
              { label: 'محصولات مشابه', icon: 'grid_view', href: routes.productSimilar(product.slug) },
            ].map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="paper-card flex items-center justify-between gap-sm rounded-lg p-md transition-shadow hover:shadow-soft"
              >
                <span className="flex items-center gap-sm text-label-md font-medium text-primary">
                  <Icon name={link.icon} size={20} />
                  {link.label}
                </span>
                <Icon name="chevron_left" size={18} className="text-outline" />
              </Link>
            ))}
          </nav>

          <div className="flex flex-col gap-sm">
            <details open className="paper-card rounded-lg px-lg py-md">
              <summary className="flex cursor-pointer list-none items-center justify-between text-label-md font-label-md text-primary">
                درباره محصول
                <Icon name="expand_more" size={20} />
              </summary>
              <p className="mt-md text-body-md leading-relaxed text-on-surface-variant">
                {product.description ??
                  `${product.title} از برند ${product.brand}، انتخابی مناسب برای علاقه‌مندان به ${product.categoryName}. کیفیت ساخت و جزئیات این محصول با استانداردهای بوژان بررسی و تأیید شده است.`}
              </p>
            </details>

            <details className="paper-card rounded-lg px-lg py-md">
              <summary className="flex cursor-pointer list-none items-center justify-between text-label-md font-label-md text-primary">
                مشخصات محصول
                <Icon name="expand_more" size={20} />
              </summary>
              <dl className="mt-md flex flex-col gap-sm">
                {specs.map((spec) => (
                  <div
                    key={spec.label}
                    className="flex items-center justify-between gap-md border-b border-paper-border pb-sm last:border-0 last:pb-0"
                  >
                    <dt className="text-caption text-on-surface-variant">{spec.label}</dt>
                    <dd className="text-body-md text-on-surface">{spec.value}</dd>
                  </div>
                ))}
              </dl>
            </details>

            <details className="paper-card rounded-lg px-lg py-md">
              <summary className="flex cursor-pointer list-none items-center justify-between text-label-md font-label-md text-primary">
                ارسال و مرجوعی
                <Icon name="expand_more" size={20} />
              </summary>
              <ul className="mt-md flex flex-col gap-sm text-body-md text-on-surface-variant">
                <li className="flex items-center gap-sm">
                  <Icon name="local_shipping" size={18} className="text-primary" />
                  ارسال به سراسر ایران، تحویل ۲ تا ۵ روز کاری
                </li>
                <li className="flex items-center gap-sm">
                  <Icon name="assignment_return" size={18} className="text-primary" />
                  امکان مرجوعی تا ۷ روز پس از تحویل
                </li>
                <li className="flex items-center gap-sm">
                  <Icon name="verified" size={18} className="text-primary" />
                  ضمانت اصالت کالا
                </li>
              </ul>
            </details>
          </div>
        </div>
      </div>

      {related.length > 0 && (
        <section className="flex flex-col gap-lg">
          <SectionHeader title="محصولات مرتبط" />
          <ProductRail products={related} />
        </section>
      )}
    </Container>
  );
}
