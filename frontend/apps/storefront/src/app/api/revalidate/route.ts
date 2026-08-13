import { revalidateTag } from 'next/cache';
import { NextResponse } from 'next/server';

/**
 * Drops cached reads for the tags an operator's save just invalidated.
 *
 * Every fetch in `lib/api` declares a tag and sets a revalidate window —
 * five minutes for the catalogue, an hour for the magazine and the category
 * tree. Nothing in either app had ever called `revalidateTag`, so those tags
 * were decoration and the windows were the whole of the story: a product that
 * came back into stock stayed "ناموجود" for five minutes, and an article
 * published in the panel did not appear on the site for an hour. Both were
 * reported as things that did not work, which from where the operator sits is
 * exactly what they were.
 *
 * The panel calls this after a write it knows the storefront caches. It is a
 * separate app in a separate container, so this has to be a request rather than
 * a function call — `revalidateTag` only reaches the cache of the process that
 * runs it.
 *
 * Authenticated with `API_KEY`, which both containers already hold and which
 * never leaves the compose network. No new secret to configure, and nothing
 * here reads the request body for anything but tag names.
 */

/** The tags this will act on. An unknown name is ignored rather than trusted. */
const KNOWN_TAGS = new Set([
  'products',
  'categories',
  'brands',
  'collections',
  'articles',
  'content-pages',
  'faqs',
  'banners',
  'store-settings',
  'loyalty',
]);

/** `product:<slug>` and the two per-product tags beside it. */
const SCOPED_TAG = /^product:[a-z0-9-]{1,200}(:reviews|:questions)?$/;

const MAX_TAGS = 30;

export async function POST(request: Request) {
  const expected = process.env.API_KEY;

  if (!expected) {
    // Refused rather than allowed. An unauthenticated cache-buster is a way to
    // make the shop fetch everything from the API on demand, which is the load
    // the caching exists to avoid.
    console.error('[revalidate] API_KEY is not set; refusing to revalidate.');
    return NextResponse.json({ error: 'revalidation is not configured' }, { status: 503 });
  }

  if (request.headers.get('x-api-key') !== expected) {
    return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  }

  const body = (await request.json().catch(() => null)) as { tags?: unknown } | null;
  const requested = Array.isArray(body?.tags) ? body.tags : [];

  const tags = requested
    .filter((tag): tag is string => typeof tag === 'string')
    .filter((tag) => KNOWN_TAGS.has(tag) || SCOPED_TAG.test(tag))
    .slice(0, MAX_TAGS);

  for (const tag of tags) revalidateTag(tag);

  return NextResponse.json({ ok: true, revalidated: tags });
}
