import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { CouponForm } from '@/components/CouponForm';

export const metadata: Metadata = { title: 'کد تخفیف جدید' };

/** Screen 128 — Add a coupon. */
export default function NewCouponPage() {
  return (
    <AdminPage
      title="کد تخفیف جدید"
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'کدهای تخفیف', href: '/coupons' },
        { label: 'کد جدید' },
      ]}
    >
      <CouponForm />
    </AdminPage>
  );
}
