/**
 * Helpers for the data this site publishes to machines rather than to people —
 * JSON-LD, the sitemap and `robots.txt`.
 *
 * The site URL was read from `process.env` in three places with the same
 * fallback spelled out each time; it lives here now so a deployment that
 * forgets `NEXT_PUBLIC_SITE_URL` fails in one place rather than three.
 */

/** Origin the site is served from. Trailing slash stripped so callers can concatenate. */
export const siteUrl = (process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000').replace(
  /\/$/,
  '',
);

/**
 * A same-site path as an absolute URL.
 *
 * Next resolves relative values in `metadata` against `metadataBase`, but
 * nothing resolves them inside a JSON-LD block — schema.org `URL` properties
 * have to carry the origin themselves or consumers drop them.
 */
export function absoluteUrl(path: string): string {
  return `${siteUrl}${path.startsWith('/') ? path : `/${path}`}`;
}

/**
 * Prices in Rial, for structured data only.
 *
 * Every price in this system is an integer number of Toman — the frontend's
 * `Money` type, the API's, and the database column behind it. ISO 4217, which
 * `priceCurrency` must use, has a code for Rial (`IRR`) and none for Toman, so
 * the amount is converted rather than the currency relabelled. Declaring the
 * Toman figure as `IRR` is what advertised every product at a tenth of its
 * price to shopping engines.
 */
export const RIAL_PER_TOMAN = 10;

export function toRial(toman: number): number {
  return toman * RIAL_PER_TOMAN;
}
