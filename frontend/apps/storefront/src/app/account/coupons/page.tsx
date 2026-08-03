import type { Metadata } from 'next';
import { EmptyState, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { CouponCard } from '@/components/account/CouponCard';
import { getCoupons } from '@/lib/api/activity';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'کدهای تخفیف من',
  robots: { index: false },
};

/** Screen 59 — My coupons. */
export default async function CouponsPage() {
  const coupons = await getCoupons();
  const now = Date.now();

  const active = coupons.filter(
    (coupon) => !coupon.used && new Date(coupon.expiresAt).getTime() >= now,
  );
  const inactive = coupons.filter(
    (coupon) => coupon.used || new Date(coupon.expiresAt).getTime() < now,
  );

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <PageHeader title="کدهای تخفیف من" backHref={routes.account} />

      <section className="flex flex-col gap-md">
        <div className="flex items-baseline justify-between gap-md">
          <h2 className="font-headline text-display-md text-primary">کدهای فعال</h2>
          <span className="tabular text-caption text-outline">
            {toPersianDigits(active.length)} کد موجود
          </span>
        </div>

        {active.length > 0 ? (
          <div className="flex flex-col gap-md">
            {active.map((coupon) => (
              <CouponCard key={coupon.id} coupon={coupon} />
            ))}
          </div>
        ) : (
          <EmptyState
            icon="sell"
            title="کد تخفیف فعالی ندارید"
            description="با خرید از بوژان و عضویت در باشگاه مشتریان، کدهای تخفیف دریافت می‌کنید."
          />
        )}
      </section>

      {inactive.length > 0 && (
        <section className="flex flex-col gap-md">
          <h2 className="font-headline text-display-md text-on-surface-variant">
            کدهای منقضی یا استفاده‌شده
          </h2>
          <div className="flex flex-col gap-md">
            {inactive.map((coupon) => (
              <CouponCard key={coupon.id} coupon={coupon} />
            ))}
          </div>
        </section>
      )}
    </Container>
  );
}
