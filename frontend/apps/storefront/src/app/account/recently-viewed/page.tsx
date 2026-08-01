import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { RecentlyViewedGrid } from '@/components/account/RecentlyViewedGrid';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'محصولات دیده‌شده اخیر',
  robots: { index: false },
};

/**
 * Screen 57 — Recently viewed products.
 *
 * The list is the shopper's own browsing history, recorded as they open
 * products, so it comes from the store rather than from this page.
 */
export default function RecentlyViewedPage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader
        title="محصولات دیده‌شده"
        backHref={routes.account}
        subtitle="محصولاتی که اخیراً مشاهده کرده‌اید اینجا نمایش داده می‌شوند."
      />

      <RecentlyViewedGrid />
    </Container>
  );
}
