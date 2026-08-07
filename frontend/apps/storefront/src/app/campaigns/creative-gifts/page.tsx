import type { Metadata } from 'next';
import { CampaignLanding } from '@/components/campaign/CampaignLanding';
import { getNewArrivals, getProducts } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

/*
 * Rendered on request, not at build.
 *
 * This page reads the catalogue, and the catalogue lives behind the API — which
 * does not exist when the image is built. Prerendering it meant `next build`
 * fetching from a host that is not up yet, which is exactly how the Docker
 * build failed. The alternative, emitting it with whatever an unreachable API
 * returns, is worse: the first visitors after a deploy would be served an empty
 * shop until the first revalidation filled it in.
 *
 * Nothing is lost by it. The fetches underneath already declare their own
 * `revalidate` window, so the API is not called per request either way — the
 * caching just happens a layer down, where stock and prices can expire on their
 * own schedule instead of being frozen into the image.
 */
export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'هدیه‌های خلاق',
  description:
    'مجموعه‌ای دست‌چین‌شده از اشیاء زیبا و کاربردی برای کسانی که به جزئیات اهمیت می‌دهند.',
};

/** Screen 49 — Creative gifts campaign landing. */
export default async function CreativeGiftsPage() {
  const [{ items }, rail] = await Promise.all([
    getProducts({ category: 'gift-lifestyle', pageSize: 8 }),
    getNewArrivals(8),
  ]);

  return (
    <CampaignLanding
      title="هدیه‌هایی برای آدم‌های خلاق"
      intro="مجموعه‌ای دست‌چین‌شده از اشیاء زیبا و کاربردی برای کسانی که به جزئیات اهمیت می‌دهند. هدیه‌ای که داستانی برای گفتن دارد."
      cover={items[0]?.image ?? ''}
      ctaLabel="مشاهده هدیه‌ها"
      ctaHref={routes.gifts}
      shortcutsTitle="برای چه کسی هدیه می‌گیرید؟"
      shortcuts={[
        { label: 'برای همکار', icon: 'work', href: routes.category('stationery') },
        { label: 'برای هنرمند', icon: 'palette', href: routes.category('art-tools') },
        { label: 'برای خانه', icon: 'chair', href: routes.category('gift-lifestyle') },
        { label: 'برای خودم', icon: 'favorite', href: routes.bestsellers },
      ]}
      featuredTitle="هدیه‌های منتخب"
      featured={items}
      railTitle="تازه رسیده‌ها"
      rail={rail}
    />
  );
}
