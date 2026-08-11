import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { NotificationComposer } from '@/components/NotificationComposer';
import { getCustomers } from '@/lib/api/customers';
import { getWebPushSettings } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'ارسال اعلان و پیامک' };

/** Screen 129 — Send notifications and SMS. */
export default async function NotificationsComposePage() {
  // Enough of the customer list to size the audience picker's recipient
  // estimate — the send itself resolves the real audience server-side.
  const [{ items: customers }, push] = await Promise.all([
    getCustomers({ pageSize: 500 }),
    getWebPushSettings(),
  ]);

  return (
    <AdminPage
      title="ارسال اعلان و پیامک"
      description="پیام را برای یک گروه مشخص ارسال کنید. ارسال انبوه قابل بازگشت نیست."
      breadcrumbs={[{ label: 'کمپین‌ها', href: '/campaigns' }, { label: 'ارسال اعلان و پیامک' }]}
    >
      <NotificationComposer
        customerGroups={customers.map((customer) => customer.group)}
        pushEnabled={push.enabled}
      />
    </AdminPage>
  );
}
