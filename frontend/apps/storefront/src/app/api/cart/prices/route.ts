import { NextResponse } from 'next/server';
import { getCompareProducts } from '@/lib/api/activity';
import { getProductSkus } from '@/lib/api/catalog';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/**
 * Current price and stock for the lines a browser is holding.
 *
 * The cart lives in `localStorage`, so a line keeps whatever the product cost
 * on the day it was added. A basket left for a week showed last week's prices,
 * a basket left for a month showed last month's, and a line for something since
 * sold out still offered it — none of which is what the shopper is about to be
 * charged, because the API re-prices every order from the catalogue when it is
 * placed. This is what lets the cart agree with that in advance rather than at
 * the payment screen.
 *
 * A slug that no longer resolves comes back absent rather than as an error: the
 * cart drops that line, which is the honest outcome for a product that is gone.
 * A product that still exists but has sold out comes back with `stock: 0`, which
 * is a different thing and is not the same answer — the line stays in the
 * basket, marked, and stops being charged for. Dropping it silently would take
 * something out of somebody's cart without telling them.
 */

const MAX_LINES = 50;

interface PricedLine {
  slug: string;
  skuId?: string;
  price: number;
  /**
   * The list price the catalogue currently shows this at, when it is on sale.
   *
   * Sent so a product that went on sale *after* it was put in the basket shows
   * its discount there. Without it the cart took the new, lower `price` and
   * kept whatever `compareAtPrice` the line was added with — usually none — so
   * the shopper saw a sale price with nothing to say it was one, and the
   * summary's savings line was short by the whole discount.
   *
   * Read from the product even for a line that named a SKU, which is what
   * adding to the basket already does: the two have to agree, or the same line
   * renders one way when it is added and another way once it is repriced.
   */
  compareAtPrice?: number;
  stock: number;
}

export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'cart-prices'), 60, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as {
    lines?: Array<{ slug?: unknown; skuId?: unknown }>;
  } | null;

  const asked = Array.isArray(body?.lines) ? body.lines.slice(0, MAX_LINES) : [];

  const wanted = asked
    .map((line) => ({
      slug: typeof line?.slug === 'string' ? line.slug : '',
      skuId: typeof line?.skuId === 'string' ? line.skuId : undefined,
    }))
    .filter((line) => line.slug.length > 0 && line.slug.length <= 200);

  if (wanted.length === 0) return NextResponse.json({ lines: [] });

  try {
    const slugs = [...new Set(wanted.map((line) => line.slug))];
    const products = await getCompareProducts(slugs);
    const bySlug = new Map(products.map((product) => [product.slug, product]));

    // Only for the lines that actually chose a combination. A cart of plain
    // products costs one upstream call in total; a variant line costs one more
    // each, and there are rarely more than a couple.
    const skuSlugs = [...new Set(wanted.filter((line) => line.skuId).map((line) => line.slug))];
    const skusBySlug = new Map(
      await Promise.all(
        skuSlugs.map(async (slug) => [slug, await getProductSkus(slug)] as const),
      ),
    );

    const priced: PricedLine[] = [];

    for (const line of wanted) {
      const product = bySlug.get(line.slug);
      if (!product) continue;

      if (line.skuId) {
        const sku = skusBySlug.get(line.slug)?.find((candidate) => candidate.id === line.skuId);
        if (!sku) continue;
        priced.push({
          slug: line.slug,
          skuId: sku.id,
          price: sku.price,
          // The combination's own list price, not the product's. A line priced
          // from the SKU and struck through against the product showed a
          // saving that belonged to a different size — and this route is what
          // re-prices a basket on every visit, so it re-applied the error even
          // after the line was added correctly.
          ...(sku.compareAt ? { compareAtPrice: sku.compareAt } : null),
          stock: sku.stock,
        });
        continue;
      }

      priced.push({
        slug: line.slug,
        price: product.price,
        ...(product.compareAtPrice ? { compareAtPrice: product.compareAtPrice } : null),
        stock: product.stock,
      });
    }

    return NextResponse.json({ lines: priced });
  } catch {
    // The cart keeps what it has. Showing a stale price is worse than showing
    // none, but emptying somebody's basket because one request failed is worse
    // than both.
    return NextResponse.json({ lines: [] });
  }
}
