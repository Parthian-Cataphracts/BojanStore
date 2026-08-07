import type { Metadata } from 'next';
import { CampaignLanding } from '@/components/campaign/CampaignLanding';
import { getBestsellers, getProducts } from '@/lib/api/catalog';
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
  title: 'شروع سال تحصیلی',
  description:
    'مجموعه‌ای برگزیده از لوازم‌التحریر و ملزومات مدرسه برای ذهن‌های خلاق و جویای هنر.',
};

/** Screen 48 — Back-to-school campaign landing. */
export default async function BackToSchoolPage() {
  const [{ items }, rail] = await Promise.all([
    getProducts({ category: 'notebooks', pageSize: 8 }),
    getBestsellers(8),
  ]);

  return (
    <CampaignLanding
      title="شروع سال تحصیلی با انتخاب‌های خلاق"
      intro="مجموعه‌ای برگزیده از لوازم‌التحریر و ملزومات مدرسه برای ذهن‌های خلاق و جویای هنر."
      cover={items[0]?.image ?? ''}
      ctaLabel="مشاهده محصولات مدرسه"
      ctaHref={`${routes.products}?category=notebooks`}
      shortcutsTitle="دسته‌بندی‌ها"
      shortcuts={[
        { label: 'دفتر و پلنر', icon: 'book', href: routes.category('notebooks') },
        { label: 'نوشت‌افزار', icon: 'edit', href: routes.category('stationery') },
        { label: 'ابزار هنری', icon: 'palette', href: routes.category('art-tools') },
        { label: 'کوله و جامدادی', icon: 'backpack', href: routes.category('gift-lifestyle') },
      ]}
      featuredTitle="انتخاب‌های سال تحصیلی"
      featured={items}
      railTitle="پرفروش‌ترین‌های این فصل"
      rail={rail}
    />
  );
}
