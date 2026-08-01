import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Code } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { OrderStatusControl } from '@/components/OrderStatusControl';
import { getOrder } from '@/lib/api/orders';
import type { AdminOrderStatus } from '@/lib/types';

export const metadata: Metadata = { title: 'تغییر وضعیت سفارش' };

/** Screen 95 — Change order status. */
export default async function OrderStatusPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const order = await getOrder(id);
  if (!order) notFound();

  return (
    <AdminPage
      title="تغییر وضعیت سفارش"
      description="تغییر وضعیت برای مشتری پیامک می‌شود، پس پیش از ثبت مطمئن شوید."
      breadcrumbs={[
        { label: 'سفارش‌ها', href: '/orders' },
        { label: order.number, href: `/orders/${order.id}` },
        { label: 'تغییر وضعیت' },
      ]}
    >
      <p className="flex items-center gap-xs text-body-md text-on-surface-variant">
        سفارش <Code>#{order.number}</Code> — {order.customer}
      </p>

      <OrderStatusControl orderId={order.id} current={order.status as AdminOrderStatus} />
    </AdminPage>
  );
}
