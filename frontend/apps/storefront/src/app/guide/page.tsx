import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { buyingGuidePage } from '@/lib/content/pages';
import { getContentPage, pageSlugs } from '@/lib/api/pages';

export const metadata: Metadata = {
  title: 'راهنمای خرید',
  description: 'راهنمای گام‌به‌گام خرید از فروشگاه اینترنتی بوژان.',
};

/** Screen 45 — راهنمای خرید. */
export default async function Page() {
  // The shop's own version when it has written one, and the copy this app
  // shipped with when it has not — see `lib/api/pages.ts`.
  const data = await getContentPage(pageSlugs.buyingGuide, buyingGuidePage);

  return <ContentPage data={data} />;
}
