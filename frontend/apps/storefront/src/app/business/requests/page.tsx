import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Card, Code, EmptyState, Icon, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getB2BRequests } from '@/lib/api/business';
import { b2bStatusMeta } from '@/lib/mock/business';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'پیگیری درخواست سازمانی',
  robots: { index: false },
};

/** Screen 64 — Track business requests. */
export default async function BusinessRequestsPage() {
  const requests = await getB2BRequests();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="پیگیری درخواست سازمانی"
        backHref={routes.business}
        subtitle="وضعیت سفارش‌ها و پیش‌فاکتورهای خود را مشاهده کنید."
      />

      {requests.length > 0 ? (
        <ul className="flex flex-col gap-md">
          {requests.map((request) => {
            const status = b2bStatusMeta[request.status];
            return (
              <li key={request.id}>
                <Card className="flex flex-col gap-sm p-lg">
                  <div className="flex flex-wrap items-center justify-between gap-sm">
                    <span className="flex items-center gap-xs text-caption text-on-surface-variant">
                      شناسه: <Code>#{request.code}</Code>
                    </span>
                    <Badge tone={status.tone}>
                      <Icon name={status.icon} size={16} />
                      {status.label}
                    </Badge>
                  </div>

                  <h2 className="text-body-lg font-semibold text-primary">{request.title}</h2>

                  <p className="tabular text-caption text-on-surface-variant">
                    {request.organization} · {toPersianDigits(request.itemCount)} قلم · ثبت:{' '}
                    {formatDate(request.createdAt, 'long')}
                  </p>

                  <div className="flex flex-col gap-sm pt-sm sm:flex-row">
                    <Link
                      href={routes.businessRequest(request.id)}
                      className={buttonClasses({ variant: 'outline', size: 'sm', fullWidth: true })}
                    >
                      مشاهده وضعیت
                    </Link>

                    {request.quoteId && (
                      <Link
                        href={routes.businessQuoteDetail(request.quoteId)}
                        className={buttonClasses({ size: 'sm', fullWidth: true })}
                      >
                        مشاهده پیش‌فاکتور
                      </Link>
                    )}
                  </div>
                </Card>
              </li>
            );
          })}
        </ul>
      ) : (
        <EmptyState
          icon="assignment"
          title="درخواستی ثبت نشده است"
          description="برای شروع، یک درخواست پیش‌فاکتور ثبت کنید."
          action={
            <Link href={routes.businessQuote} className={buttonClasses()}>
              درخواست پیش‌فاکتور
            </Link>
          }
        />
      )}
    </Container>
  );
}
