import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { shippingPage } from '@/lib/content/pages';
import { getContentPage, pageSlugs } from '@/lib/api/pages';

export const metadata: Metadata = {
  title: 'شرایط ارسال',
  description: 'روش‌های ارسال، هزینه‌ها و زمان‌بندی تحویل سفارش در بوژان.',
};

/** Screen 43 — شرایط ارسال. */
export default async function Page() {
  // The shop's own version when it has written one, and the copy this app
  // shipped with when it has not — see `lib/api/pages.ts`.
  const data = await getContentPage(pageSlugs.shipping, shippingPage);

  return <ContentPage data={data} />;
}
