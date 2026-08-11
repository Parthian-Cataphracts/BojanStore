import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import Link from 'next/link';
import {
  Badge,
  Card,
  Icon,
  buttonClasses,
  formatNumber,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { getLoyaltyProgramme } from '@/lib/api/store';
import { getCurrentUserIfSignedIn } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'باشگاه مشتریان بوژان',
  description: 'با هر خرید امتیاز جمع کنید و از مزایای اختصاصی باشگاه مشتریان بوژان بهره‌مند شوید.',
};

/** Screen 50 — Loyalty club. */
export default async function LoyaltyPage() {
  // Null for a visitor who is not signed in. This page is public — it is how
  // the club is advertised — so it has to render for someone who has no
  // standing yet, and the standing card becomes an invitation instead.
  const [user, club] = await Promise.all([getCurrentUserIfSignedIn(), getLoyaltyProgramme()]);
  const points = user?.loyaltyPoints ?? 0;

  // A shop that has configured no tiers runs no club, and says so rather than
  // advertising one. This page used to hold three tiers of its own and promise
  // a permanent discount that nothing anywhere applied.
  if (!club.enabled) notFound();

  const tiers = club.tiers;

  // The highest rung the member's points reach — the same reading the checkout
  // does when it prices their order, so the tier shown here is the tier charged.
  const currentIndex = tiers.reduce(
    (best, tier, index) => (points >= tier.minimumPoints ? index : best),
    -1,
  );
  const current = currentIndex >= 0 ? tiers[currentIndex] : undefined;
  const next = tiers[currentIndex + 1];
  const progress = next?.minimumPoints
    ? Math.min(100, Math.round((points / next.minimumPoints) * 100))
    : 100;

  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <section className="flex flex-col items-center gap-md text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="workspace_premium" size={32} />
        </span>
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-hero">
          باشگاه مشتریان بوژان
        </h1>
        <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant md:text-body-lg">
          به دنیای پاداش‌ها و تجربه‌های خاص خوش آمدید. با هر خرید امتیاز جمع کنید و از مزایای
          اختصاصی بهره‌مند شوید.
        </p>
      </section>

      {/* Current standing, or an invitation to have one. */}
      {user ? (
        <Card className="flex flex-col gap-md p-xl shadow-soft">
          <div className="flex flex-wrap items-center justify-between gap-md">
            <div className="flex flex-col gap-xs">
              <span className="text-caption text-on-surface-variant">امتیاز شما</span>
              <span className="tabular font-headline text-kpi-mobile text-primary md:text-kpi">
                {formatNumber(points)}
              </span>
            </div>
            {current && <Badge tone="mint">سطح {current.name}</Badge>}
          </div>

          {next && (
            <div className="flex flex-col gap-xs">
              <div className="flex items-center justify-between text-caption text-on-surface-variant">
                <span>تا سطح {next.name}</span>
                <span className="tabular">
                  {formatNumber(Math.max(0, next.minimumPoints - points))} امتیاز مانده
                </span>
              </div>
              <span
                role="img"
                aria-label={`${toPersianDigits(progress)}٪ تا سطح بعدی`}
                className="h-2 overflow-hidden rounded-full bg-surface-container-high"
              >
                <span
                  className="block h-full rounded-full bg-primary transition-all"
                  style={{ width: `${progress}%` }}
                />
              </span>
            </div>
          )}
        </Card>
      ) : (
        <Card className="flex flex-col items-center gap-md p-xl text-center shadow-soft">
          <h2 className="font-headline text-section-title text-primary">امتیازهای شما اینجاست</h2>
          <p className="max-w-lg text-body-md leading-loose text-on-surface-variant">
            برای دیدن امتیاز و سطح عضویت خود وارد حساب کاربری شوید. عضویت رایگان است و امتیازها از
            همان اولین خرید جمع می‌شوند.
          </p>
          <Link
            href={`${routes.login}?next=${encodeURIComponent(routes.loyalty)}`}
            className={buttonClasses({ size: 'lg', className: 'gap-sm' })}
          >
            ورود یا ثبت‌نام
            <Icon name="login" size={20} />
          </Link>
        </Card>
      )}

      {/* Tiers */}
      <section className="flex flex-col gap-lg">
        <h2 className="font-headline text-section-title text-primary">سطح‌های عضویت</h2>

        <div className="grid gap-gutter md:grid-cols-2 lg:grid-cols-3">
          {tiers.map((tier, index) => (
            <Card
              key={tier.name}
              className={`flex flex-col gap-md p-lg ${index === currentIndex ? 'border-primary shadow-soft' : ''}`}
            >
              <div className="flex items-center justify-between gap-sm">
                <h3 className="text-card-title text-primary">{tier.name}</h3>
                {index === currentIndex && <Badge tone="mint">سطح فعلی شما</Badge>}
              </div>

              <span className="tabular text-caption text-on-surface-variant">
                از {formatNumber(tier.minimumPoints)} امتیاز
              </span>

              {/* Only what the checkout will actually do. These used to be
                  free text on this page — "۵٪ تخفیف دائمی", "ارسال رایگان
                  نامحدود" — with nothing behind either of them. */}
              <ul className="flex flex-col gap-sm">
                {tier.discountPercent > 0 && (
                  <li className="flex items-start gap-sm">
                    <Icon name="check_circle" size={18} className="mt-1 shrink-0 text-primary-fixed-dim" />
                    <span className="text-body-md leading-relaxed text-on-surface-variant">
                      {toPersianDigits(tier.discountPercent)}٪ تخفیف روی هر سفارش
                    </span>
                  </li>
                )}

                {tier.freeShipping && (
                  <li className="flex items-start gap-sm">
                    <Icon name="check_circle" size={18} className="mt-1 shrink-0 text-primary-fixed-dim" />
                    <span className="text-body-md leading-relaxed text-on-surface-variant">
                      ارسال رایگان روی همه‌ی سفارش‌ها
                    </span>
                  </li>
                )}

                {tier.discountPercent === 0 && !tier.freeShipping && (
                  <li className="text-body-md leading-relaxed text-on-surface-variant">
                    شروع عضویت — با خرید بعدی به سطح بالاتر می‌رسید.
                  </li>
                )}
              </ul>
            </Card>
          ))}
        </div>
      </section>

      {/* How to earn.
          One rule, because one is what the shop actually does. This section
          used to advertise four — purchases, reviews, referrals and a birthday
          bonus — and the shop had no referral feature, nothing that runs on a
          birthday, and no code anywhere that awarded a point for anything. */}
      {club.tomanPerPoint > 0 && (
        <section className="flex flex-col gap-lg">
          <h2 className="font-headline text-section-title text-primary">چطور امتیاز جمع کنیم؟</h2>

          <Card className="flex flex-col items-center gap-sm p-lg text-center">
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
              <Icon name="shopping_bag" size={24} />
            </span>
            <h3 className="text-card-title text-primary">خرید</h3>
            <p className="text-body-md leading-relaxed text-on-surface-variant">
              به ازای هر {formatPrice(club.tomanPerPoint)} خرید، {toPersianDigits(1)} امتیاز. امتیاز
              پس از تحویل سفارش به حساب شما اضافه می‌شود.
            </p>
          </Card>
        </section>
      )}

      <div className="flex flex-col gap-md sm:flex-row sm:justify-center">
        <Link href={routes.coupons} className={buttonClasses({ size: 'lg', className: 'px-xl' })}>
          کدهای تخفیف من
        </Link>
        <Link
          href={routes.products}
          className={buttonClasses({ variant: 'outline', size: 'lg', className: 'px-xl' })}
        >
          شروع خرید
        </Link>
      </div>
    </Container>
  );
}
