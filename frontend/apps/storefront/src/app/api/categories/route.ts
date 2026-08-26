import { NextResponse } from 'next/server';
import { getCategories } from '@/lib/api/catalog';

/**
 * The category tree, for the header's hover menu.
 *
 * Its own route for the same reason the search box has one: the menu is a
 * client component and the catalogue sits behind a credential the browser does
 * not hold. It is also why the header does not simply read the tree on the
 * server — the header lives in the root layout, so a fetch there would run for
 * every statically generated page at build time, against an API that is not up
 * while the image is being built.
 *
 * Only the fields the menu draws are returned — names, an icon and the
 * children. The counts are left behind with the images: the panel is a list to
 * be scanned by name, and «۹ محصول» beside thirty rows is a table nobody reads.
 */
export const dynamic = 'force-dynamic';

export interface MenuCategory {
  slug: string;
  name: string;
  icon: string;
  children: { slug: string; name: string }[];
}

export async function GET() {
  try {
    const categories = await getCategories();

    return NextResponse.json(
      categories.map((category) => ({
        slug: category.slug,
        name: category.name,
        icon: category.icon,
        children: (category.children ?? []).map((child) => ({
          slug: child.slug,
          name: child.name,
        })),
      })) satisfies MenuCategory[],
      // The tree changes when the catalogue is re-organised, which is rarely.
      // A shopper moving around the site should not re-fetch it per page.
      { headers: { 'Cache-Control': 'public, max-age=600' } },
    );
  } catch {
    // An empty list draws no panel, and the nav item underneath is still the
    // link to the categories page it always was.
    return NextResponse.json([] satisfies MenuCategory[], {
      headers: { 'Cache-Control': 'no-store' },
    });
  }
}
