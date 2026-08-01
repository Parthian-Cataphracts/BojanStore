import type { Metadata } from 'next';
import { CampaignLanding } from '@/components/campaign/CampaignLanding';
import { getBestsellers, getProducts } from '@/lib/api/catalog';
import { routes } from '@/lib/routes';

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
