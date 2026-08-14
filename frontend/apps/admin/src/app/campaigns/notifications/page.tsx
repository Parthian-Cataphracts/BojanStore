import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { NotificationCenter } from '@/components/NotificationCenter';
import { getAccounts, getSentNotifications } from '@/lib/api/customers';
import { getWebPushSettings } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'ارسال اعلان' };
export const dynamic = 'force-dynamic';

/**
 * Screen 129 — ارسال اعلان.
 *
 * Compose and history on one screen. It was a broadcast form on its own: no way
 * to write to a single customer from here, and no record of what had already
 * been sent, so an operator could not tell whether a message had gone out
 * without asking the person who sent it.
 *
 * `customerId` in the query preselects a recipient, which is how the link from
 * a customer's record arrives.
 */
export default async function NotificationsComposePage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const targetId = Array.isArray(params.customerId) ? params.customerId[0] : params.customerId;

  const [accounts, sent, push] = await Promise.all([
    getAccounts({ pageSize: 500 }),
    getSentNotifications({ pageSize: 100 }),
    getWebPushSettings(),
  ]);

  return (
    <AdminPage
      title="ارسال اعلان"
      description="پیام خصوصی به یک کاربر، یا اعلان عمومی برای همه‌ی مشتریان. ارسال انبوه قابل بازگشت نیست."
      breadcrumbs={[{ label: 'کمپین‌ها', href: '/campaigns' }, { label: 'ارسال اعلان' }]}
    >
      <NotificationCenter
        accounts={accounts.items}
        sent={sent.items}
        pushEnabled={push.enabled}
        initialCustomerId={targetId}
      />
    </AdminPage>
  );
}
