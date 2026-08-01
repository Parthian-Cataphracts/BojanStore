import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { CouponForm } from '@/components/CouponForm';
import { getCoupon } from '@/lib/api/coupons';

export const metadata: Metadata = { title: 'ویرایش کد تخفیف' };

/** Screen 128 — Edit a coupon. */
export default async function EditCouponPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const coupon = await getCoupon(id);
  if (!coupon) notFound();

  return (
    <AdminPage
      title="ویرایش کد تخفیف"
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'کدهای تخفیف', href: '/coupons' },
        { label: coupon.code },
      ]}
    >
      <CouponForm coupon={coupon} />
    </AdminPage>
  );
}
