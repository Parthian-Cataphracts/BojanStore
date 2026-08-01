import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Badge, Card, Code, Icon, buttonClasses, cn, formatDate, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getB2BRequest } from '@/lib/api/business';
import { b2bStatusMeta } from '@/lib/mock/business';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'وضعیت درخواست پیش‌فاکتور',
  robots: { index: false },
};

/** Screen 70 — Quote request status. */
export default async function BusinessRequestStatusPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const request = await getB2BRequest(id);
  if (!request) notFound();

  const status = b2bStatusMeta[request.status];

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="وضعیت درخواست پیش‌فاکتور" backHref={routes.businessRequests} />

      <Card className="flex flex-wrap items-center justify-between gap-md p-lg">
        <div className="flex flex-col gap-xs">
          <span className="text-caption text-on-surface-variant">شناسه درخواست</span>
          <Code className="text-body-lg font-semibold text-primary">#{request.code}</Code>
          <span className="tabular text-caption text-outline">
            {request.organization} · {toPersianDigits(request.itemCount)} قلم
          </span>
        </div>
        <Badge tone={status.tone}>
          <Icon name={status.icon} size={16} />
          {status.label}
        </Badge>
      </Card>

      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-section-title text-primary">مراحل پیگیری</h2>

        <Card className="p-lg">
          <ol className="flex flex-col">
            {request.timeline.map((step, index) => {
              const done = step.state === 'done';
              const current = step.state === 'current';
              const isLast = index === request.timeline.length - 1;

              return (
                <li key={step.id} className="relative flex gap-md pb-lg last:pb-0">
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
                    <Icon name={done ? 'check' : 'radio_button_unchecked'} size={20} />
                  </span>

                  <div className="flex min-w-0 flex-col gap-xs pt-1">
                    <h3
                      className={cn(
                        'text-body-md',
                        done || current ? 'font-semibold text-primary' : 'text-on-surface-variant',
                      )}
                    >
                      {step.label}
                    </h3>
                    {done && (
                      <p className="tabular text-caption text-outline">
                        {formatDate(request.createdAt, 'long')}
                      </p>
                    )}
                  </div>
                </li>
              );
            })}
          </ol>
        </Card>
      </section>

      <div className="flex flex-col gap-md sm:flex-row">
        {request.quoteId && (
          <Link
            href={routes.businessQuoteDetail(request.quoteId)}
            className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm' })}
          >
            <Icon name="receipt_long" size={20} />
            مشاهده پیش‌فاکتور
          </Link>
        )}
        <Link
          href={routes.businessConsultant}
          className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
        >
          تماس با مشاور فروش
        </Link>
      </div>
    </Container>
  );
}
