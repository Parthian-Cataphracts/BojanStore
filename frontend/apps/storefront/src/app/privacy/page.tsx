import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { privacyPage } from '@/lib/content/pages';

export const metadata: Metadata = {
  title: 'حریم خصوصی',
  description: 'سیاست حریم خصوصی و نحوه نگهداری اطلاعات کاربران در بوژان.',
};

/** Screen 42 — حریم خصوصی. */
export default function Page() {
  return <ContentPage data={privacyPage} />;
}
