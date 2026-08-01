import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { StockMovementForm } from '@/components/inventory/StockMovementForm';
import { getProducts } from '@/lib/api/products';

export const metadata: Metadata = { title: 'ثبت خروج / اصلاح موجودی' };

/** Screen 114 — Record outgoing stock or a correction. */
export default async function StockAdjustPage() {
  const { items } = await getProducts({ pageSize: 200 });

  return (
    <AdminPage
      title="ثبت خروج / اصلاح موجودی"
      description="خروج کالا بابت ضایعات، مرجوعی به تأمین‌کننده یا اصلاح شمارش را اینجا ثبت کنید."
      breadcrumbs={[
        { label: 'موجودی و انبار', href: '/inventory' },
        { label: 'ثبت خروج / اصلاح موجودی' },
      ]}
    >
      <StockMovementForm kind="out" products={items} />
    </AdminPage>
  );
}
