import type { ResourceKey } from './resources';

/**
 * Telling the storefront that something it caches has changed.
 *
 * The storefront caches its reads by tag — five minutes for the catalogue, an
 * hour for the magazine, the category tree and the brand directory — and
 * nothing had ever invalidated one. The tags were written, the windows were
 * enforced, and the only thing that ever cleared a tag was time. So a product
 * restocked from the inventory screen went on saying «ناموجود» to shoppers, and
 * an article published here did not appear in the magazine until the hour was
 * up. Neither is a caching trade-off anybody chose; both read as the feature
 * being broken, and were reported as exactly that.
 *
 * A request rather than a call, because the two are separate apps in separate
 * containers and `revalidateTag` only reaches the process that runs it.
 */

/**
 * Which storefront tags each panel write invalidates.
 *
 * Only resources the storefront actually caches appear. An order status, a
 * support reply or an operator account changes nothing a shopper's page holds,
 * so those write nothing here rather than clearing tags for the sake of it.
 */
const TAGS_BY_RESOURCE: Partial<Record<ResourceKey, readonly string[]>> = {
  products: ['products'],
  'product-pricing': ['products'],
  'product-discount': ['products'],
  'product-variants': ['products'],
  'product-skus': ['products'],
  'product-attributes': ['products'],
  'product-volume-tiers': ['products'],
  // The one an operator is most likely to be standing over waiting for.
  'stock-movements': ['products'],
  // A category page lists products, so its own tag is not enough on its own.
  categories: ['categories', 'products'],
  brands: ['brands', 'products'],
  collections: ['collections'],
  /*
    One resource covers four kinds — article, page, faq, banner — and the body
    does not always say which. Dropping all four costs one extra fetch each on
    the next visit and removes the case where an operator publishes a page and
    is told, by the site itself, that they did not.
  */
  content: ['articles', 'content-pages', 'faqs', 'banners'],
  // Its own resource now, and its own tag: an article saved here is the one the
  // magazine renders, so the magazine's cache is what has to go.
  articles: ['articles'],
  settings: ['store-settings'],
  'shipping-methods': ['store-settings'],
  loyalty: ['loyalty'],
};

/**
 * The storefront's address on the container network.
 *
 * Set in the compose file. Absent on a developer's machine, where the two apps
 * are usually not both running — so the absence is silent and the caller does
 * nothing, rather than logging on every save.
 */
const STOREFRONT = process.env.STOREFRONT_INTERNAL_URL;

/** How long the panel will wait for it. */
const TIMEOUT_MS = 3000;

/**
 * Best effort, deliberately.
 *
 * A save that has already been committed by the API must not be reported as
 * failed because the storefront was slow to answer, or is not running at all.
 * The worst case if this does nothing is the behaviour that existed before it:
 * the tag expires on its own.
 */
export async function revalidateStorefront(resource: ResourceKey, slug?: string): Promise<void> {
  const tags = TAGS_BY_RESOURCE[resource];
  if (!tags || !STOREFRONT) return;

  const key = process.env.API_KEY;
  if (!key) {
    console.warn('[revalidate] API_KEY is not set; the storefront cache will expire on its own.');
    return;
  }

  try {
    await fetch(`${STOREFRONT.replace(/\/$/, '')}/api/revalidate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': key },
      // The product's own page as well as the listing, when the write named
      // one: a shopper sitting on the product is the person most likely to be
      // looking at the stale number.
      body: JSON.stringify({ tags: slug ? [...tags, `product:${slug}`] : tags }),
      cache: 'no-store',
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
  } catch (cause) {
    console.warn(
      '[revalidate] could not reach the storefront; its cache will expire on its own.',
      cause instanceof Error ? cause.message : cause,
    );
  }
}
