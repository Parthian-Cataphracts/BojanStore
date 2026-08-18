import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import {
  Badge,
  Breadcrumb,
  Icon,
  Price,
  Rating,
  SectionHeader,
  formatPrice,
  serializeJsonLd,
  toPersianDigits,
} from '@bojan/ui';
import Link from 'next/link';
import { buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { RecordProductView } from '@/components/product/RecordProductView';
import { ProductPurchase } from '@/components/product/ProductPurchase';
import { ProductGallery } from '@/components/product/ProductGallery';
import { ProductTabs } from '@/components/product/ProductTabs';
import { ProductRail } from '@/components/product/ProductGrid';
import {
  getProduct,
  getProductQuestions,
  getProductReviews,
  getProducts,
  getProductSkus,
  getRatingBreakdown,
  getRelatedProducts,
  getVariantAxes,
} from '@/lib/api/catalog';
import { bestFreeShippingOffer, getShippingMethods } from '@/lib/api/cart';
import { getStoreSettings } from '@/lib/api/store';
import { routes } from '@/lib/routes';
import { absoluteUrl, toRial } from '@/lib/seo';
import { staticParams } from '@/lib/static-params';
import type { RatingBreakdown } from '@/lib/api/types';

export async function generateStaticParams() {
  return staticParams('products', async () =>
    (await getProducts({ pageSize: 100 })).items.map((product) => ({ slug: product.slug })),
  );
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

/** What the tabs show when the review service is unreachable — no stars, no invented ones. */
const NO_REVIEWS: RatingBreakdown = {
  average: 0,
  total: 0,
  counts: { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 },
};

/** Screen 06 — Product detail. */
export default async function ProductPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;

  /*
    Reviews, the star breakdown and the questions join the batch, because the
    tabs below show them in place rather than linking away to three screens.
    Each falls back rather than throwing: they are secondary to buying, and a
    product page that 500s because the questions endpoint hiccuped is a product
    nobody can buy over something nobody asked.
  */
  const [
    product,
    related,
    variantAxes,
    skus,
    { promises },
    shippingMethods,
    reviews,
    breakdown,
    questions,
  ] = await Promise.all([
    getProduct(slug),
    getRelatedProducts(slug, 8),
    getVariantAxes(slug),
    getProductSkus(slug),
    getStoreSettings(),
    getShippingMethods(),
    getProductReviews(slug).catch(() => []),
    getRatingBreakdown(slug).catch(() => NO_REVIEWS),
    getProductQuestions(slug).catch(() => []),
  ]);

  const freeShippingOffer = bestFreeShippingOffer(shippingMethods);
  if (!product) notFound();

  const images = product.gallery?.length ? product.gallery : [product.image];
  const specs = product.specs ?? [
    { label: 'برند', value: product.brand },
    { label: 'دسته‌بندی', value: product.categoryName },
    { label: 'کد کالا', value: toPersianDigits(product.id.replace(/\D/g, '')) },
  ];

  const description =
    product.description ??
    `${product.title} از برند ${product.brand}، انتخابی مناسب برای علاقه‌مندان به ${product.categoryName}. کیفیت ساخت و جزئیات این محصول با استانداردهای بوژان بررسی و تأیید شده است.`;

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
    <Container className="gap-xl py-lg md:pb-xl md:py-xl flex flex-col pb-[168px]">
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

      {/*
        The buying half: image on one side, the decision on the other.

        Nothing else lives here now. It used to also carry four link tiles and
        three accordions, which is what made the column a stack of collapsed
        summaries — everything about the product was in this half of the screen
        and none of it was legible. Those moved under the fold to `ProductTabs`,
        where they get the full width.
      */}
      <div className="gap-xl md:gap-lg lg:gap-xl grid md:grid-cols-2">
        <div className="gap-md flex flex-col">
          <ProductGallery images={images} alt={product.imageAlt || product.title} />

          {images.length > 1 && (
            <Link
              href={routes.productGallery(product.slug)}
              className="text-caption text-on-surface-variant hover:text-primary gap-xs flex items-center self-start transition-colors"
            >
              <Icon name="photo_library" size={18} />
              دیدن {toPersianDigits(images.length)} تصویر در گالری
            </Link>
          )}
        </div>

        {/* Sticky from `lg` up, where the gallery is tall enough for the price
            and the button to otherwise scroll away from the picture they are
            about. Below that the two are stacked and there is nothing to pin. */}
        <div className="gap-lg lg:top-24 flex h-fit flex-col lg:sticky">
          <header className="gap-sm flex flex-col">
            <span className="text-caption text-outline">{product.brand}</span>
            <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
              {product.title}
            </h1>

            <div className="gap-md flex flex-wrap items-center">
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
              className={`gap-xs text-caption flex items-center ${
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

          {/* The three lines somebody wants before deciding, not after. The
              detail behind each is in the «ارسال و مرجوعی» tab below. */}
          <ul className="gap-sm paper-card p-md text-caption text-on-surface-variant flex flex-col rounded-lg">
            <li className="gap-sm flex items-center">
              <Icon name="local_shipping" size={18} className="text-primary shrink-0" />
              ارسال به سراسر ایران، تحویل {promises.deliveryEstimate}
            </li>
            <li className="gap-sm flex items-center">
              <Icon name="assignment_return" size={18} className="text-primary shrink-0" />
              مرجوعی تا {toPersianDigits(promises.returnWindowDays)} روز پس از تحویل
            </li>
            <li className="gap-sm flex items-center">
              <Icon name="verified" size={18} className="text-primary shrink-0" />
              ضمانت اصالت کالا
            </li>
          </ul>
        </div>
      </div>

      {/*
        Full width, deliberately — it starts at the edge the image starts at and
        runs to the far edge of the column beside it. That width is what makes
        a description readable and a specification table two columns instead of
        a squeezed one.
      */}
      <ProductTabs
        slug={product.slug}
        description={description}
        specs={specs}
        reviews={reviews}
        breakdown={breakdown}
        questions={questions}
        shipping={{
          deliveryEstimate: promises.deliveryEstimate,
          returnWindowDays: promises.returnWindowDays,
          // The best offer the shop actually has, read from its shipping
          // methods. It used to come from a figure on the settings screen that
          // nothing in the checkout consulted — so this promised free delivery
          // the shop then charged for.
          freeShippingLabel:
            freeShippingOffer === null
              ? null
              : freeShippingOffer === 0
                ? 'روی همه‌ی سفارش‌ها'
                : `برای خرید بالای ${formatPrice(freeShippingOffer)}`,
        }}
      />

      {related.length > 0 && (
        <section className="gap-lg flex flex-col">
          <SectionHeader
            title="محصولات مشابه"
            subtitle="پیشنهادهایی از همان دسته‌بندی"
            actionLabel="مشاهده همه"
            actionHref={routes.productSimilar(product.slug)}
          />
          <ProductRail products={related} />
        </section>
      )}
    </Container>
  );
}
