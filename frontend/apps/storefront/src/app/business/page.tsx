import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { b2bAudiences, b2bProcessSteps } from '@/lib/mock/business';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'خرید سازمانی',
  description:
    'تأمین محصولات خلاق برای تیم‌ها، مدارس و سازمان‌ها؛ هدایای سازمانی، تجهیزات اداری و خرید عمده با شرایط ویژه.',
};

const entries = [
  {
    title: 'درخواست پیش‌فاکتور',
    body: 'فرم را پر کنید تا کارشناس فروش با شما تماس بگیرد.',
    icon: 'request_quote',
    href: routes.businessQuote,
  },
  {
    title: 'فرم خرید عمده',
    body: 'اقلام و تعداد مورد نیاز خود را مشخص کنید.',
    icon: 'inventory',
    href: routes.businessBulk,
  },
  {
    title: 'بسته‌های هدیه شرکتی',
    body: 'بسته‌های آماده برای قدردانی از همکاران و شرکا.',
    icon: 'redeem',
    href: routes.businessGiftBoxes,
  },
  {
    title: 'پیگیری درخواست‌ها',
    body: 'وضعیت درخواست‌ها و پیش‌فاکتورهای خود را ببینید.',
    icon: 'fact_check',
    href: routes.businessRequests,
  },
];

/** Screen 20 — Business landing. */
export default function BusinessPage() {
  return (
    <Container className="flex flex-col gap-xl py-lg md:py-xl">
      <section className="flex flex-col items-center gap-md text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="business_center" size={32} />
        </span>
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-hero">
          تأمین محصولات خلاق برای تیم‌ها، مدارس و سازمان‌ها
        </h1>
        <p className="max-w-3xl text-body-md leading-loose text-on-surface-variant md:text-body-lg">
          هدایای سازمانی خاص، تجهیزات اداری با طراحی مینیمال، و تأمین یکپارچه لوازم‌التحریر برای
          محیط‌های آموزشی و حرفه‌ای با شرایط ویژه.
        </p>

        <div className="flex flex-wrap justify-center gap-sm pt-sm">
          {b2bAudiences.map((audience) => (
            <span
              key={audience}
              className="rounded-full bg-surface-container px-md py-sm text-label-md font-medium text-on-surface"
            >
              {audience}
            </span>
          ))}
        </div>

        <div className="mt-md flex flex-col gap-md sm:flex-row">
          <Link href={routes.businessQuote} className={buttonClasses({ size: 'lg', className: 'px-xl' })}>
            درخواست پیش‌فاکتور
          </Link>
          <Link
            href={routes.businessConsultant}
            className={buttonClasses({ variant: 'outline', size: 'lg', className: 'px-xl' })}
          >
            تماس با مشاور فروش
          </Link>
        </div>
      </section>

      <section className="grid gap-gutter sm:grid-cols-2">
        {entries.map((entry) => (
          <Link key={entry.href} href={entry.href} className="group block">
            <Card className="flex h-full items-start gap-md p-lg transition-shadow group-hover:shadow-soft">
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                <Icon name={entry.icon} size={24} />
              </span>
              <div className="flex min-w-0 flex-1 flex-col gap-xs">
                <h2 className="text-card-title text-primary-container">{entry.title}</h2>
                <p className="text-body-md leading-relaxed text-on-surface-variant">{entry.body}</p>
              </div>
              <Icon name="chevron_left" size={20} className="mt-1 shrink-0 text-outline" />
            </Card>
          </Link>
        ))}
      </section>

      <section className="flex flex-col gap-lg">
        <h2 className="font-headline text-section-title text-primary">چرا بوژان سازمانی؟</h2>
        <div className="grid gap-gutter sm:grid-cols-3">
          {[
            { icon: 'verified', title: 'قیمت‌گذاری سازمانی', body: 'تخفیف پلکانی بر اساس تعداد.' },
            { icon: 'receipt_long', title: 'فاکتور رسمی', body: 'با کد اقتصادی و امکان تسویه اعتباری.' },
            { icon: 'design_services', title: 'سفارشی‌سازی', body: 'حکاکی لوگو و بسته‌بندی اختصاصی.' },
          ].map((item) => (
            <Card key={item.title} className="flex flex-col items-center gap-sm p-lg text-center">
              <span className="flex h-12 w-12 items-center justify-center rounded-full bg-soft-mint text-primary">
                <Icon name={item.icon} size={24} />
              </span>
              <h3 className="text-card-title text-primary">{item.title}</h3>
              <p className="text-body-md leading-relaxed text-on-surface-variant">{item.body}</p>
            </Card>
          ))}
        </div>
      </section>

      <section className="flex flex-col gap-lg">
        <div className="flex items-baseline justify-between gap-md">
          <h2 className="font-headline text-section-title text-primary">فرآیند ثبت سفارش</h2>
          <Link
            href={routes.businessGuide}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            راهنمای کامل
          </Link>
        </div>

        <div className="grid gap-gutter sm:grid-cols-2 lg:grid-cols-3">
          {b2bProcessSteps.slice(0, 3).map((step) => (
            <Card key={step.title} className="flex flex-col gap-sm p-lg">
              <Icon name={step.icon} size={26} className="text-primary" />
              <h3 className="text-card-title text-primary">{step.title}</h3>
              <p className="text-body-md leading-relaxed text-on-surface-variant">{step.body}</p>
            </Card>
          ))}
        </div>
      </section>
    </Container>
  );
}
