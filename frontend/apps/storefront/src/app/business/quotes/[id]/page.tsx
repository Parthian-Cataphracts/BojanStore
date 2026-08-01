import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Badge, Card, Code, Icon, buttonClasses, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getQuote } from '@/lib/api/business';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'جزئیات پیش‌فاکتور',
  robots: { index: false },
};

const statusMeta = {
  pending: { label: 'در انتظار تایید', tone: 'warning' as const },
  approved: { label: 'تایید شده', tone: 'mint' as const },
  expired: { label: 'منقضی شده', tone: 'error' as const },
};

/** Screen 65 — Quote detail. */
export default async function QuoteDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const quote = await getQuote(id);
  if (!quote) notFound();

  const status = statusMeta[quote.status];

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="جزئیات پیش‌فاکتور" backHref={routes.businessRequests} />

      <Card className="flex flex-col gap-md p-lg">
        <div className="flex flex-wrap items-start justify-between gap-md">
          <div className="flex flex-col gap-xs">
            <span className="text-caption text-on-surface-variant">شماره پیش‌فاکتور</span>
            <Code className="text-body-lg font-semibold text-primary">{quote.number}</Code>
          </div>
          <Badge tone={status.tone}>{status.label}</Badge>
        </div>

        <dl className="grid gap-md border-t border-paper-border pt-md sm:grid-cols-3">
          {[
            { icon: 'domain', label: 'سازمان درخواست‌دهنده', value: quote.organization },
            { icon: 'person', label: 'مشاور فروش', value: quote.salesRep },
            {
              icon: 'calendar_today',
              label: 'تاریخ اعتبار',
              value: `تا ${formatDate(quote.validUntil, 'long')}`,
            },
          ].map((row) => (
            <div key={row.label} className="flex items-start gap-sm">
              <Icon name={row.icon} size={20} className="mt-px shrink-0 text-primary" />
              <div className="flex min-w-0 flex-col gap-xs">
                <dt className="text-caption text-on-surface-variant">{row.label}</dt>
                <dd className="text-body-md text-on-surface">{row.value}</dd>
              </div>
            </div>
          ))}
        </dl>
      </Card>

      <Card surface="plain" className="overflow-hidden">
        <h2 className="border-b border-outline-variant/40 px-lg py-md font-headline text-card-title text-primary">
          اقلام سفارش
        </h2>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[560px] text-start">
            <thead className="bg-surface-container-low text-on-surface-variant">
              <tr>
                <th scope="col" className="px-lg py-sm text-start">کالا</th>
                <th scope="col" className="px-lg py-sm text-start">کد</th>
                <th scope="col" className="px-lg py-sm text-start">تعداد</th>
                <th scope="col" className="px-lg py-sm text-start">قیمت واحد</th>
                <th scope="col" className="px-lg py-sm text-start">جمع</th>
              </tr>
            </thead>
            <tbody>
              {quote.lines.map((line) => (
                <tr key={line.sku} className="border-t border-outline-variant/30">
                  <td className="px-lg py-md text-on-surface">{line.title}</td>
                  <td className="px-lg py-md">
                    <Code className="text-caption text-on-surface-variant">{line.sku}</Code>
                  </td>
                  <td className="tabular px-lg py-md text-on-surface-variant">
                    {toPersianDigits(line.quantity)}
                  </td>
                  <td className="tabular px-lg py-md text-on-surface-variant">
                    {formatPrice(line.unitPrice)}
                  </td>
                  <td className="tabular px-lg py-md font-medium text-primary">
                    {formatPrice(line.unitPrice * line.quantity)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card className="flex flex-col gap-sm p-lg">
        <dl className="flex flex-col gap-sm text-body-md">
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">جمع اقلام</dt>
            <dd className="tabular text-on-surface">{formatPrice(quote.subtotal)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">تخفیف سازمانی</dt>
            <dd className="tabular text-secondary">−{formatPrice(quote.discount)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">مالیات بر ارزش افزوده (۹٪)</dt>
            <dd className="tabular text-on-surface">{formatPrice(quote.tax)}</dd>
          </div>
          <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
            <dt className="text-body-lg font-semibold text-primary">مبلغ قابل پرداخت</dt>
            <dd className="tabular text-body-lg font-semibold text-primary">
              {formatPrice(quote.total)}
            </dd>
          </div>
        </dl>
      </Card>

      <div className="flex flex-col gap-md sm:flex-row">
        <Link
          href={routes.businessConsultant}
          className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm' })}
        >
          <Icon name="check_circle" size={20} />
          تایید و ادامه
        </Link>
        <Link
          href={routes.businessConsultant}
          className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
        >
          درخواست اصلاح
        </Link>
      </div>
    </Container>
  );
}
