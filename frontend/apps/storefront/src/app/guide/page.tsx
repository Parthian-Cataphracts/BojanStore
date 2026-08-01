import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { buyingGuidePage } from '@/lib/content/pages';

export const metadata: Metadata = {
  title: 'راهنمای خرید',
  description: 'راهنمای گام‌به‌گام خرید از فروشگاه اینترنتی بوژان.',
};

/** Screen 45 — راهنمای خرید. */
export default function Page() {
  return <ContentPage data={buyingGuidePage} />;
}
