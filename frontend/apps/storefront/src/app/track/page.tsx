import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { TrackOrderForm } from '@/components/status/TrackOrderForm';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'پیگیری سفارش',
  description: 'وضعیت سفارش خود را با شماره سفارش و شماره موبایل در بوژان پیگیری کنید.',
};

/** Screen 30 — Order tracking. */
export default function TrackPage() {
  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <header className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
          وضعیت سفارش شما
        </h1>
        <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant">
          برای مشاهده آخرین وضعیت سفارش خود، شماره سفارش و شماره موبایل ثبت‌شده را وارد کنید.
        </p>
      </header>

      <TrackOrderForm />

      <Card className="flex flex-wrap items-center justify-between gap-md p-lg">
        <div className="flex items-center gap-md">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-soft-mint text-primary">
            <Icon name="support_agent" size={24} />
          </span>
          <div className="flex flex-col gap-xs">
            <span className="text-label-md font-label-md text-primary">نیاز به راهنمایی دارید؟</span>
            <span className="text-caption text-on-surface-variant">
              تیم پشتیبانی ما آماده پاسخگویی است.
            </span>
          </div>
        </div>

        <Link href={routes.contact} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
          تماس با پشتیبانی
        </Link>
      </Card>
    </Container>
  );
}
