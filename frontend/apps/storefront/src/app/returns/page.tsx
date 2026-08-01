import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { returnsPage } from '@/lib/content/pages';

export const metadata: Metadata = {
  title: 'شرایط مرجوعی',
  description: 'شرایط و مراحل بازگرداندن کالا در فروشگاه بوژان.',
};

/** Screen 44 — شرایط مرجوعی. */
export default function Page() {
  return <ContentPage data={returnsPage} />;
}
