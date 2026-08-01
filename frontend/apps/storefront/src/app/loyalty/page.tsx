import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Card, Icon, buttonClasses, formatNumber, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { getCurrentUser } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'باشگاه مشتریان بوژان',
  description: 'با هر خرید امتیاز جمع کنید و از مزایای اختصاصی باشگاه مشتریان بوژان بهره‌مند شوید.',
};

/** Membership tiers, ordered by the points needed to reach them. */
const tiers = [
  { name: 'برنزی', threshold: 0, perks: ['ارسال رایگان بالای ۱ میلیون', 'دسترسی به کدهای فصلی'] },
  { name: 'نقره‌ای', threshold: 1_000, perks: ['۵٪ تخفیف دائمی', 'ارسال رایگان بالای ۵۰۰ هزار'] },
  { name: 'طلایی', threshold: 3_000, perks: ['۱۰٪ تخفیف دائمی', 'ارسال رایگان نامحدود', 'پشتیبانی اختصاصی'] },
];

const earnRules = [
  { icon: 'shopping_bag', title: 'خرید', body: 'به ازای هر ۱۰ هزار تومان خرید، ۱ امتیاز.' },
  { icon: 'rate_review', title: 'ثبت نظر', body: 'برای هر نظر تاییدشده، ۵۰ امتیاز.' },
  { icon: 'group_add', title: 'معرفی دوستان', body: 'برای هر دعوت موفق، ۲۰۰ امتیاز.' },
  { icon: 'cake', title: 'تولد', body: 'در ماه تولدتان، ۳۰۰ امتیاز هدیه.' },
];

/** Screen 50 — Loyalty club. */
export default async function LoyaltyPage() {
  const user = await getCurrentUser();

  // The current tier is the highest one the member has already reached.
  const currentIndex = tiers.reduce(
    (best, tier, index) => (user.loyaltyPoints >= tier.threshold ? index : best),
    0,
  );
  const current = tiers[currentIndex]!;
  const next = tiers[currentIndex + 1];
  const progress = next
    ? Math.min(100, Math.round((user.loyaltyPoints / next.threshold) * 100))
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

      {/* Current standing */}
      <Card className="flex flex-col gap-md p-xl shadow-soft">
        <div className="flex flex-wrap items-center justify-between gap-md">
          <div className="flex flex-col gap-xs">
            <span className="text-caption text-on-surface-variant">امتیاز شما</span>
            <span className="tabular font-headline text-kpi-mobile text-primary md:text-kpi">
              {formatNumber(user.loyaltyPoints)}
            </span>
          </div>
          <Badge tone="mint">سطح {current.name}</Badge>
        </div>

        {next && (
          <div className="flex flex-col gap-xs">
            <div className="flex items-center justify-between text-caption text-on-surface-variant">
              <span>تا سطح {next.name}</span>
              <span className="tabular">
                {formatNumber(Math.max(0, next.threshold - user.loyaltyPoints))} امتیاز مانده
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

      {/* Tiers */}
      <section className="flex flex-col gap-lg">
        <h2 className="font-headline text-section-title text-primary">سطح‌های عضویت</h2>

        <div className="grid gap-gutter md:grid-cols-3">
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
                از {formatNumber(tier.threshold)} امتیاز
              </span>

              <ul className="flex flex-col gap-sm">
                {tier.perks.map((perk) => (
                  <li key={perk} className="flex items-start gap-sm">
                    <Icon name="check_circle" size={18} className="mt-1 shrink-0 text-primary-fixed-dim" />
                    <span className="text-body-md leading-relaxed text-on-surface-variant">
                      {perk}
                    </span>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      </section>

      {/* How to earn */}
      <section className="flex flex-col gap-lg">
        <h2 className="font-headline text-section-title text-primary">چطور امتیاز جمع کنیم؟</h2>

        <div className="grid gap-gutter sm:grid-cols-2 lg:grid-cols-4">
          {earnRules.map((rule) => (
            <Card key={rule.title} className="flex flex-col items-center gap-sm p-lg text-center">
              <span className="flex h-12 w-12 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                <Icon name={rule.icon} size={24} />
              </span>
              <h3 className="text-card-title text-primary">{rule.title}</h3>
              <p className="text-body-md leading-relaxed text-on-surface-variant">{rule.body}</p>
            </Card>
          ))}
        </div>
      </section>

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
