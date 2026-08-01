import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { StockMovementForm } from '@/components/inventory/StockMovementForm';
import { getProducts } from '@/lib/api/products';

export const metadata: Metadata = { title: 'ثبت ورود کالا' };

/** Screen 113 — Record incoming stock. */
export default async function StockReceivePage() {
  const { items } = await getProducts({ pageSize: 200 });

  return (
    <AdminPage
      title="ثبت ورود کالا"
      description="ورود کالا از تأمین‌کننده، بازگشت از مشتری یا اصلاح شمارش انبار را اینجا ثبت کنید."
      breadcrumbs={[{ label: 'موجودی و انبار', href: '/inventory' }, { label: 'ثبت ورود کالا' }]}
    >
      <StockMovementForm kind="in" products={items} />
    </AdminPage>
  );
}
