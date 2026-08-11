/**
 * The informational pages, as the owner wrote them.
 *
 * Terms, privacy, the shipping and returns policies, the buying and sizing
 * guides. All of it was compiled into this bundle — a shop could not change one
 * word of the terms its customers agree to by buying, or correct a delivery
 * promise, without a developer and a deploy.
 *
 * The shipped copy is still here and is still what an unedited shop shows. That
 * is deliberate: a shop launches with policies rather than with six empty
 * pages, and the moment the owner saves one in the panel it takes over.
 */

import { api, useMockData } from './client';
import { faqs as shippedFaqs } from '../content/pages';
import type { ContentPageData } from '../content/pages';

/**
 * The slugs the panel writes and these pages read.
 *
 * Named here rather than typed as strings at each call site, so a page and the
 * entry an operator creates for it cannot end up spelled differently — which
 * would look exactly like the CMS not working.
 */
export const pageSlugs = {
  terms: 'terms',
  privacy: 'privacy',
  shipping: 'shipping',
  returns: 'returns',
  buyingGuide: 'buying-guide',
  sizeGuide: 'size-guide',
} as const;

export type PageSlug = (typeof pageSlugs)[keyof typeof pageSlugs];

interface StoredPage {
  slug: string;
  title: string;
  excerpt?: string | null;
  body: string;
  updatedAt: string;
}

const PAGE_REVALIDATE = 3600;

/**
 * Turns the prose an owner typed into the shape these screens render.
 *
 * A line beginning `##` starts a new section, which is the one convention worth
 * asking for: a returns policy is a document with headings, and the alternative
 * is either one undifferentiated wall of text or a structured editor nobody
 * wants to fill in. Anything before the first heading is the intro.
 *
 * Blank lines separate paragraphs — which is how anyone writes into a textarea
 * without being told to.
 */
export function parseContentBody(body: string, fallbackTitle: string): Pick<ContentPageData, 'intro' | 'blocks'> {
  const sections: { title?: string; paragraphs: string[] }[] = [{ paragraphs: [] }];

  for (const chunk of body.split(/\n{2,}/)) {
    const text = chunk.trim();
    if (text.length === 0) continue;

    const heading = /^#{2,3}\s+(.*)$/.exec(text);

    if (heading) {
      sections.push({ title: heading[1]!.trim(), paragraphs: [] });
      continue;
    }

    sections.at(-1)!.paragraphs.push(text);
  }

  const [lead, ...rest] = sections;

  // With no headings at all the whole document is the lead, and using its first
  // paragraph as an intro and the remainder as a body reads the way the shipped
  // pages do.
  const blocks =
    rest.length > 0
      ? [
          ...(lead!.paragraphs.length > 1
            ? [{ title: fallbackTitle, body: lead!.paragraphs.slice(1) }]
            : []),
          ...rest.map((section) => ({ title: section.title!, body: section.paragraphs })),
        ]
      : lead!.paragraphs.length > 1
        ? [{ body: lead!.paragraphs.slice(1) }]
        : [];

  return {
    intro: lead!.paragraphs[0] ?? '',
    blocks: blocks.filter((block) => block.body.length > 0),
  };
}

/**
 * The stored page, or the shipped one.
 *
 * A page the shop has not written is a 404 from the API, which is not an error
 * here — it is the ordinary case, and it means "use what you have".
 */
export async function getContentPage(
  slug: PageSlug,
  fallback: ContentPageData,
): Promise<ContentPageData> {
  if (useMockData) return fallback;

  const stored = await api
    .get<StoredPage>(`/pages/${slug}`, {
      next: { revalidate: PAGE_REVALIDATE, tags: ['content-pages'] },
    })
    .catch(() => null);

  if (!stored || stored.body.trim().length === 0) return fallback;

  const title = stored.title.trim().length > 0 ? stored.title : fallback.title;
  const { intro, blocks } = parseContentBody(stored.body, title);

  return {
    title,
    intro: stored.excerpt?.trim() || intro,
    blocks,
  };
}

/** One question as the storefront renders it. */
export interface Faq {
  question: string;
  answer: string;
  category: string;
}

/**
 * The shop's own questions, or the ones this app shipped with.
 *
 * The panel has had an FAQ editor since screen 125 and nothing read what it
 * wrote: every question an operator added went nowhere, and every question a
 * customer read was compiled into the bundle.
 *
 * An empty list from the API means the shop has not written any, which is the
 * ordinary case on day one — the shipped set stands in rather than leaving the
 * page bare.
 */
export async function getFaqs(): Promise<Faq[]> {
  if (useMockData) return [...shippedFaqs];

  const stored = await api
    .get<Faq[]>('/faqs', { next: { revalidate: PAGE_REVALIDATE, tags: ['faqs'] } })
    .catch(() => null);

  return stored && stored.length > 0 ? stored : [...shippedFaqs];
}

/** The banner slugs the storefront asks for. */
export const bannerSlugs = { homeHero: 'home-hero' } as const;

export interface Banner {
  title: string;
  subtitle: string;
  imageUrl: string;
}

/**
 * A banner the owner set, or null.
 *
 * Null rather than a fallback object: the caller has its own shipped hero and
 * knows how to render it, and returning a half-filled banner would put an empty
 * heading over the shop's largest picture.
 */
export async function getBanner(slug: string): Promise<Banner | null> {
  if (useMockData) return null;

  return api
    .get<Banner>(`/banners/${slug}`, {
      next: { revalidate: PAGE_REVALIDATE, tags: ['banners'] },
    })
    .catch(() => null);
}
