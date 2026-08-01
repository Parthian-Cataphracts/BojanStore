import type { Metadata } from 'next';
import { CampaignLanding } from '@/components/campaign/CampaignLanding';
import { getNewArrivals, getProducts } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

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
