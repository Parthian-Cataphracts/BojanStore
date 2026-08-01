import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { shippingPage } from '@/lib/content/pages';

export const metadata: Metadata = {
  title: 'شرایط ارسال',
  description: 'روش‌های ارسال، هزینه‌ها و زمان‌بندی تحویل سفارش در بوژان.',
};

/** Screen 43 — شرایط ارسال. */
export default function Page() {
  return <ContentPage data={shippingPage} />;
}
