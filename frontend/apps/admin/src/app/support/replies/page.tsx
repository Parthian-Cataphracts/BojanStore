import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { CannedReplyManager } from '@/components/CannedReplyManager';
import { getCannedReplies } from '@/lib/api/support';

export const metadata: Metadata = { title: 'پاسخ‌های آماده پشتیبانی' };
export const dynamic = 'force-dynamic';

/**
 * Screen 132 — Canned support replies.
 *
 * The list, the row controls and the form all live in one client component:
 * adding, editing and deleting change the same list, and splitting them would
 * mean three components arguing about who owns it.
 */
export default async function CannedRepliesPage() {
  const cannedReplies = await getCannedReplies();

  return (
    <AdminPage
      title="پاسخ‌های آماده پشتیبانی"
      description="پاسخ‌های پرتکرار را اینجا نگه دارید تا کارشناسان با یک کلیک از آن‌ها استفاده کنند."
      breadcrumbs={[{ label: 'پشتیبانی', href: '/support' }, { label: 'پاسخ‌های آماده' }]}
    >
      <CannedReplyManager initial={cannedReplies} />
    </AdminPage>
  );
}
