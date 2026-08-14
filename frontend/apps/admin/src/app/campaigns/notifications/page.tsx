import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { CustomerNotifyPanel } from '@/components/CustomerNotifyPanel';
import { NotificationComposer } from '@/components/NotificationComposer';
import { getCustomer, getCustomers } from '@/lib/api/customers';
import { getWebPushSettings } from '@/lib/api/providers';

export const metadata: Metadata = { title: 'ارسال اعلان و پیامک' };

/** Screen 129 — Send notifications and SMS. */
export default async function NotificationsComposePage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  // Arriving from a customer's record with that customer already chosen. The
  // per-customer message used to be a card on their page; it is here now, with
  // every other outbound message the shop sends.
  const params = await searchParams;
  const targetId = Array.isArray(params.customerId) ? params.customerId[0] : params.customerId;
  const target = targetId ? await getCustomer(targetId) : null;
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
      {target && (
        <CustomerNotifyPanel customerId={target.id} customerName={target.name} />
      )}

      <NotificationComposer
        customerGroups={customers.map((customer) => customer.group)}
        pushEnabled={push.enabled}
      />
    </AdminPage>
  );
}
