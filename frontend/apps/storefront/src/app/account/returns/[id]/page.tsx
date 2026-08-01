import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Badge, Card, Code, Icon, buttonClasses, cn, formatDate, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getReturn } from '@/lib/api/account';
import { returnStatusMeta } from '@/lib/mock/returns';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'وضعیت مرجوعی',
  robots: { index: false },
};

/** Screen 36 — Return status. */
export default async function ReturnStatusPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const request = await getReturn(id);
  if (!request) notFound();

  const status = returnStatusMeta[request.status];

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="وضعیت مرجوعی" backHref={routes.orders} />

      {/* Summary */}
      <Card className="flex flex-col gap-md p-lg">
        <div className="flex flex-wrap items-center justify-between gap-sm">
          <span className="flex items-center gap-xs text-label-md font-label-md text-primary">
            <Icon name="receipt_long" size={20} />
            <Code>{request.code}</Code>
          </span>
          <Badge tone={status.tone}>
            <Icon name={status.icon} size={16} />
            {status.label}
          </Badge>
        </div>

        <div className="flex items-center gap-md border-t border-paper-border pt-md">
          <Link
            href={routes.product(request.productSlug)}
            className="relative h-20 w-20 shrink-0 overflow-hidden rounded-lg border border-outline-variant"
          >
            <Image
              src={request.productImage}
              alt={request.productTitle}
              fill
              sizes="80px"
              className="object-cover"
            />
          </Link>

          <div className="flex min-w-0 flex-col gap-xs">
            <h2 className="line-clamp-2 text-body-md text-on-surface">{request.productTitle}</h2>
            <span className="flex items-center gap-xs text-caption text-on-surface-variant">
              <Icon name="info" size={16} />
              دلیل: {request.reason}
            </span>
            <span className="tabular text-caption text-outline">
              تعداد: {toPersianDigits(request.quantity)} · ثبت در{' '}
              {formatDate(request.createdAt, 'long')}
            </span>
          </div>
        </div>
      </Card>

      {/* Return journey */}
      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-display-md text-primary">مسیر مرجوعی کالا</h2>

        <Card className="p-lg">
          <ol className="flex flex-col">
            {request.timeline.map((step, index) => {
              const done = step.state === 'done';
              const current = step.state === 'current';
              const isLast = index === request.timeline.length - 1;

              return (
                <li key={step.id} className="relative flex gap-md pb-lg last:pb-0">
                  {/* Rail linking this step to the next. */}
                  {!isLast && (
                    <span
                      aria-hidden="true"
                      className={cn(
                        'absolute start-[19px] top-10 h-[calc(100%-2.5rem)] w-0.5',
                        done ? 'bg-primary' : 'bg-outline-variant',
                      )}
                    />
                  )}

                  <span
                    className={cn(
                      'relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-2',
                      done && 'border-primary bg-primary text-on-primary',
                      current && 'border-primary bg-soft-mint text-primary',
                      !done && !current && 'border-outline-variant bg-surface text-outline-variant',
                    )}
                  >
                    <Icon name={done ? 'check' : step.icon} size={20} />
                  </span>

                  <div className="flex min-w-0 flex-col gap-xs pt-1">
                    <h3
                      className={cn(
                        'text-body-md',
                        done || current ? 'font-label-md text-primary' : 'text-on-surface-variant',
                      )}
                    >
                      {step.label}
                    </h3>
                    <p className="text-caption text-on-surface-variant">{step.description}</p>
                  </div>
                </li>
              );
            })}
          </ol>
        </Card>
      </section>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="sms" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          نتیجه بررسی از طریق پیامک و حساب کاربری به شما اطلاع داده می‌شود. لطفاً تا اتمام بررسی
          شکیبا باشید.
        </p>
      </Card>

      <div className="flex flex-col gap-md sm:flex-row">
        <Link
          href={routes.contact}
          className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm' })}
        >
          <Icon name="headset_mic" size={20} />
          ارتباط با پشتیبانی
        </Link>
        <Link
          href={routes.orders}
          className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
        >
          بازگشت به سفارش‌ها
        </Link>
      </div>
    </Container>
  );
}
