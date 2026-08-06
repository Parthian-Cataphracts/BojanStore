import type { MetadataRoute } from 'next';
import { getBrands, getCategories, getProducts } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';
import { absoluteUrl } from '@/lib/seo';

/**
 * The catalogue is paged through rather than asked for in one go.
 *
 * This used to request `pageSize: 500` in a single call. The API clamps a page
 * size above its own maximum of 100 back down to the *default* of 24, so the
 * sitemap silently contained the first 24 products of the shop and nothing
 * else — the rest of the catalogue was never offered for crawling at all.
 */
const PAGE_SIZE = 100;
const MAX_PAGES = 50;

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

/**
 * Every product the API will give us, a page at a time.
 *
 * Stops on the first page that fails rather than throwing: a sitemap missing
 * its last page is worth serving, and a sitemap that 500s is not — see the note
 * on the default export.
 */
async function allProductSlugs(): Promise<string[]> {
  const slugs: string[] = [];

  for (let page = 1; page <= MAX_PAGES; page += 1) {
    const result = await getProducts({ page, pageSize: PAGE_SIZE }).catch(() => null);
    if (!result || result.items.length === 0) break;

    slugs.push(...result.items.map((product) => product.slug));

    if (slugs.length >= result.total) break;
  }

  return slugs;
}

/**
 * Sitemap covering the catalogue and static content — /robots.txt points at it.
 *
 * Every call to the API is allowed to fail on its own. All three used to sit in
 * one `Promise.all` with no `catch`, so a restart or a slow query answered
 * Googlebot with a 500 — and repeated 5xx on a sitemap is what makes Search
 * Console stop reading it until someone resubmits by hand. The static routes
 * are always known, so the worst outcome is now a shorter sitemap rather than
 * no sitemap.
 */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const [productSlugs, categories, brands] = await Promise.all([
    allProductSlugs(),
    getCategories().catch(() => []),
    getBrands().catch(() => []),
  ]);

  return [
    ...staticRoutes.map((route) => ({ ...route, url: absoluteUrl(route.url) })),
    ...productSlugs.map((slug) => ({
      url: absoluteUrl(routes.product(slug)),
      changeFrequency: 'weekly' as const,
      priority: 0.6,
    })),
    ...categories.map((category) => ({
      url: absoluteUrl(routes.category(category.slug)),
      changeFrequency: 'weekly' as const,
      priority: 0.6,
    })),
    ...brands.map((brand) => ({
      url: absoluteUrl(routes.brand(brand.slug)),
      changeFrequency: 'weekly' as const,
      priority: 0.5,
    })),
  ];
}
