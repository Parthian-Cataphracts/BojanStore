import type { MetadataRoute } from 'next';
import { getBrands, getCategories, getProducts } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000';

const staticRoutes: MetadataRoute.Sitemap = [
  { url: routes.home, changeFrequency: 'daily', priority: 1 },
  { url: routes.products, changeFrequency: 'daily', priority: 0.9 },
  { url: routes.categories, changeFrequency: 'weekly', priority: 0.8 },
  { url: routes.brands, changeFrequency: 'weekly', priority: 0.6 },
  { url: routes.bestsellers, changeFrequency: 'daily', priority: 0.7 },
  { url: routes.newArrivals, changeFrequency: 'daily', priority: 0.7 },
  { url: routes.offers, changeFrequency: 'daily', priority: 0.7 },
  { url: routes.faq, changeFrequency: 'monthly', priority: 0.3 },
  { url: routes.about, changeFrequency: 'monthly', priority: 0.3 },
  { url: routes.contact, changeFrequency: 'monthly', priority: 0.3 },
];

/** Sitemap covering the catalogue and static content — /robots.txt points at it. */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const [{ items: products }, categories, brands] = await Promise.all([
    getProducts({ pageSize: 500 }),
    getCategories(),
    getBrands(),
  ]);

  const productUrls: MetadataRoute.Sitemap = products.map((product) => ({
    url: `${siteUrl}${routes.product(product.slug)}`,
    changeFrequency: 'weekly',
    priority: 0.6,
  }));

  const categoryUrls: MetadataRoute.Sitemap = categories.map((category) => ({
    url: `${siteUrl}${routes.category(category.slug)}`,
    changeFrequency: 'weekly',
    priority: 0.6,
  }));

  const brandUrls: MetadataRoute.Sitemap = brands.map((brand) => ({
    url: `${siteUrl}${routes.brand(brand.slug)}`,
    changeFrequency: 'weekly',
    priority: 0.5,
  }));

  return [
    ...staticRoutes.map((route) => ({ ...route, url: `${siteUrl}${route.url}` })),
    ...productUrls,
    ...categoryUrls,
    ...brandUrls,
  ];
}
