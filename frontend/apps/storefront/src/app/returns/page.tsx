import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { returnsPage } from '@/lib/content/pages';
import { getContentPage, pageSlugs } from '@/lib/api/pages';

export const metadata: Metadata = {
  title: 'شرایط مرجوعی',
  description: 'شرایط و مراحل بازگرداندن کالا در فروشگاه بوژان.',
};

/** Screen 44 — شرایط مرجوعی. */
export default async function Page() {
  // The shop's own version when it has written one, and the copy this app
  // shipped with when it has not — see `lib/api/pages.ts`.
  const data = await getContentPage(pageSlugs.returns, returnsPage);

  return <ContentPage data={data} />;
}
