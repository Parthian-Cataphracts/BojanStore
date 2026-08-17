import { NextResponse } from 'next/server';
import { getProducts } from '@/lib/api/catalog';

/**
 * The first few matches for what somebody is typing, and how many there are
 * altogether.
 *
 * Its own route because the suggestion box is a client component and the
 * catalogue is behind a credential the browser does not hold. It asks for one
 * page of five and hands back the count the API already returns beside them,
 * which is what lets the last row say «۴۰ نتیجه» rather than «نمایش بیشتر» and
 * leave the shopper guessing whether more exist.
 *
 * The matching itself is the ordinary product search — the same folded
 * comparison, so «ابرنگ» suggests «آبرنگ» here exactly as it finds it on the
 * results page. A separate matching rule for suggestions would be a box that
 * offers something the page it leads to cannot find.
 */
export const dynamic = 'force-dynamic';

/** Enough to be useful, few enough to read without scrolling. */
const SUGGESTIONS = 5;

export interface SearchSuggestion {
  slug: string;
  title: string;
  brand: string;
  price: number;
  image: string;
  imageAlt: string;
}

export interface SearchSuggestions {
  items: SearchSuggestion[];
  /** Every match, not just the ones returned — the "show all" row counts with it. */
  total: number;
}

const empty: SearchSuggestions = { items: [], total: 0 };

export async function GET(request: Request) {
  const term = (new URL(request.url).searchParams.get('q') ?? '').trim();

  // A single character matches most of a catalogue, which is a list of
  // everything dressed up as a suggestion. The box waits for a second one.
  if (term.length < 2) {
    return NextResponse.json(empty, { headers: { 'Cache-Control': 'no-store' } });
  }

  try {
    const page = await getProducts({ search: term, pageSize: SUGGESTIONS });

    return NextResponse.json(
      {
        total: page.total,
        items: page.items.map((product) => ({
          slug: product.slug,
          title: product.title,
          brand: product.brand,
          price: product.price,
          image: product.image,
          imageAlt: product.imageAlt,
        })),
      } satisfies SearchSuggestions,
      { headers: { 'Cache-Control': 'no-store' } },
    );
  } catch {
    // A suggestion box that throws is a search box that stops working. The
    // form underneath it still submits, and the results page is the real
    // answer anyway.
    return NextResponse.json(empty, { headers: { 'Cache-Control': 'no-store' } });
  }
}
