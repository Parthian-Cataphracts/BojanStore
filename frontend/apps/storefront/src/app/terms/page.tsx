import type { Metadata } from 'next';
import { ContentPage } from '@/components/content/ContentPage';
import { termsPage } from '@/lib/content/pages';

export const metadata: Metadata = {
  title: 'قوانین و مقررات',
  description: 'قوانین و مقررات استفاده از فروشگاه اینترنتی بوژان.',
};

/** Screen 41 — قوانین و مقررات. */
export default function Page() {
  return <ContentPage data={termsPage} />;
}
