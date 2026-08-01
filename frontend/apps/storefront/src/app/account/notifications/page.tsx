import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { NotificationList } from '@/components/account/NotificationList';
import { getNotifications } from '@/lib/api/activity';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'اعلان‌های من',
  robots: { index: false },
};

/** Screen 53 — Notifications. */
export default async function NotificationsPage() {
  const notifications = await getNotifications();

  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="اعلان‌های من" backHref={routes.account} />
      <NotificationList initial={notifications} />
    </Container>
  );
}
