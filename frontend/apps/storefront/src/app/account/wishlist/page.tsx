import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { WishlistGrid } from '@/components/account/WishlistGrid';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'علاقه‌مندی‌ها',
  robots: { index: false },
};

/**
 * Screen 11 — Wishlist.
 *
 * The list comes from the wishlist store the layout provides rather than from
 * this page: it has to agree with the heart on every catalogue card, which the
 * customer may have toggled since. The grid owns the empty state too, so
 * removing the last item does not leave an empty grid behind.
 */
export default function WishlistPage() {
  return (
    <Container className="py-lg md:py-xl">
      <PageHeader
        title="علاقه‌مندی‌ها"
        backHref={routes.account}
        subtitle="محصولاتی که برای بعد ذخیره کرده‌اید"
      />

      <WishlistGrid />
    </Container>
  );
}
