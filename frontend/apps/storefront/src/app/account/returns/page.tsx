import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Badge, Card, Code, EmptyState, Icon, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getReturns } from '@/lib/api/account';
import { returnStatusMeta } from '@/lib/mock/returns';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'مرجوعی‌های من',
  robots: { index: false },
};

/** Screen 35 — My returns. */
export default async function ReturnsPage() {
  const returns = await getReturns();

  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="مرجوعی‌های من" backHref={routes.account} />

      {returns.length > 0 ? (
        <ul className="flex flex-col gap-md">
          {returns.map((request) => {
            const status = returnStatusMeta[request.status];

            return (
              <li key={request.id}>
                <Card className="flex flex-col gap-sm p-md">
                  <div className="flex items-center justify-between border-b border-surface-variant pb-2">
                    <span className="tabular text-label-md font-label-md text-primary">
                      مرجوعی <Code>{request.code}</Code>
                    </span>
                    <Badge tone={status.tone}>
                      <Icon name={status.icon} size={16} />
                      {status.label}
                    </Badge>
                  </div>

                  <div className="flex items-center gap-md py-2">
                    <div className="relative h-16 w-16 shrink-0 overflow-hidden rounded-lg border border-outline-variant">
                      <Image
                        src={request.productImage}
                        alt={request.productTitle}
                        fill
                        sizes="64px"
                        className="object-cover"
                      />
                    </div>

                    <div className="flex min-w-0 flex-col gap-xs">
                      <h2 className="line-clamp-2 text-body-md text-on-surface">{request.productTitle}</h2>
                      <span className="tabular text-caption text-on-surface-variant">
                        سفارش <Code>#{request.orderNumber}</Code> ·{' '}
                        {toPersianDigits(request.quantity)} عدد · {formatDate(request.createdAt, 'long')}
                      </span>
                    </div>
                  </div>

                  <Link
                    href={routes.returnStatus(request.id)}
                    className={buttonClasses({ variant: 'outline', fullWidth: true, className: 'mt-2 gap-2' })}
                  >
                    مشاهده جزئیات
                    <Icon name="visibility" size={18} />
                  </Link>
                </Card>
              </li>
            );
          })}
        </ul>
      ) : (
        <EmptyState
          icon="assignment_return"
          title="مرجوعی ثبت‌شده‌ای ندارید"
          description="در صورت نیاز به مرجوعی کالا، از صفحه جزئیات سفارش اقدام کنید."
          action={
            <Link href={routes.orders} className={buttonClasses()}>
              مشاهده سفارش‌ها
            </Link>
          }
        />
      )}
    </Container>
  );
}
