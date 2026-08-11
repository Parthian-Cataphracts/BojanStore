import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { privacyPage } from '@/lib/content/pages';
import { getContentPage, pageSlugs } from '@/lib/api/pages';

export const metadata: Metadata = {
  title: 'حریم خصوصی',
  description: 'سیاست حریم خصوصی و نحوه نگهداری اطلاعات کاربران در بوژان.',
};

/** Screen 42 — حریم خصوصی. */
export default async function Page() {
  // The shop's own version when it has written one, and the copy this app
  // shipped with when it has not — see `lib/api/pages.ts`.
  const data = await getContentPage(pageSlugs.privacy, privacyPage);

  return <ContentPage data={data} />;
}
