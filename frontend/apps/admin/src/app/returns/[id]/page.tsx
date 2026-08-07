import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import {
  Badge,
  Card,
  Code,
  Icon,
  buttonClasses,
  formatDateTime,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { ReturnDecisionForm } from '@/components/ReturnDecisionForm';
import { getReturn } from '@/lib/api/returns';
import { requireRole } from '@/lib/auth/server';
import { returnStatusMeta } from '@/lib/status';

export const metadata: Metadata = { title: 'بررسی مرجوعی' };

const REFUND_METHOD_LABEL: Record<string, string> = {
  wallet: 'کیف پول',
  card: 'کارت بانکی',
  original: 'همان روش پرداخت',
};

/**
 * Screen 96's detail — one return request, and the decision on it.
 *
 * Everything the operator needs to judge it is on this page, because the
 * decision is irreversible in the direction that matters: the money leaves once
 * and the goods go back on the shelf once. The lines are priced from the order's
 * own frozen figures rather than from today's catalogue, which is why the
 * numbers here can differ from the product page and should.
 */
export default async function AdminReturnDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requireRole('owner', 'sales', 'support');

  const { id } = await params;
  const request = await getReturn(id);
  if (!request) notFound();

  const meta = returnStatusMeta[request.status];
  const units = request.items.reduce((sum, item) => sum + item.quantity, 0);
  const goods = request.items.reduce((sum, item) => sum + item.unitPrice * item.quantity, 0);

  return (
    <AdminPage
      title={`مرجوعی ${request.code}`}
      description={`ثبت‌شده در ${formatDateTime(request.createdAt)}`}
      breadcrumbs={[
        { label: 'فروش' },
        { label: 'مرجوعی‌ها', href: '/returns' },
        { label: request.code },
      ]}
      actions={
        <Link
          href={`/orders/${request.orderId}`}
          className={buttonClasses({ variant: 'outline', size: 'sm' })}
        >
          مشاهده سفارش {request.orderNumber}
        </Link>
      }
    >
      <div className="grid gap-lg lg:grid-cols-[2fr_1fr]">
        <div className="flex flex-col gap-lg">
          <Card className="flex flex-col gap-md p-lg">
            <div className="flex flex-wrap items-center justify-between gap-sm">
              <h2 className="flex items-center gap-sm font-headline text-card-title text-primary">
                <Icon name={meta.icon} size={22} />
                وضعیت درخواست
              </h2>
              <Badge tone={meta.tone}>{meta.label}</Badge>
            </div>

            <ReturnDecisionForm
              returnId={request.id}
              status={request.status}
              refundEstimate={request.refundEstimate}
              payable={request.payable}
            />
          </Card>

          <Card className="flex flex-col gap-md p-lg">
            <h2 className="font-headline text-card-title text-primary">
              اقلام مرجوعی ({toPersianDigits(units)} عدد)
            </h2>

            <ul className="flex flex-col divide-y divide-paper-border">
              {request.items.map((item) => (
                <li key={`${item.productId}-${item.slug}`} className="flex items-center gap-md py-sm">
                  {/* eslint-disable-next-line @next/next/no-img-element -- product media is remote and unoptimised by design here. */}
                  <img
                    src={item.image}
                    alt={item.title}
                    className="h-14 w-14 shrink-0 rounded-lg object-cover"
                  />
                  <span className="flex min-w-0 flex-1 flex-col gap-2xs">
                    <span className="truncate text-body-md text-on-surface">{item.title}</span>
                    <span className="text-caption text-on-surface-variant">
                      {toPersianDigits(item.quantity)} عدد ×{' '}
                      <span className="tabular">{formatPrice(item.unitPrice)}</span>
                    </span>
                  </span>
                  <span className="tabular shrink-0 text-body-md text-on-surface">
                    {formatPrice(item.unitPrice * item.quantity)}
                  </span>
                </li>
              ))}
            </ul>
          </Card>

          <Card className="flex flex-col gap-sm p-lg">
            <h2 className="font-headline text-card-title text-primary">دلیل مشتری</h2>
            <p className="text-body-md leading-loose text-on-surface">{request.reason}</p>
            {request.description && (
              <p className="whitespace-pre-line text-body-md leading-loose text-on-surface-variant">
                {request.description}
              </p>
            )}
            {request.reviewNote && (
              <p className="mt-sm rounded-lg bg-surface-container-low p-md text-body-md leading-loose text-on-surface-variant">
                <span className="block text-label-md font-medium text-on-surface">یادداشت اپراتور</span>
                {request.reviewNote}
              </p>
            )}
          </Card>
        </div>

        <div className="flex flex-col gap-lg">
          <Card className="flex flex-col gap-sm p-lg">
            <h2 className="font-headline text-card-title text-primary">مبلغ</h2>

            <Row label="ارزش کالاهای مرجوعی" value={formatPrice(goods)} />
            {/*
              The estimate is not the goods total and is not meant to be: the
              order's discount is deducted in proportion, and shipping never
              comes back because the parcel was carried and delivered. Showing
              both is what makes the difference explainable at the counter.
            */}
            <Row
              label={request.status === 'refunded' ? 'بازپرداخت‌شده' : 'مبلغ قابل بازگشت'}
              value={formatPrice(
                request.status === 'refunded' ? request.refundAmount : request.refundEstimate,
              )}
              emphasis
            />
            <Row label="روش بازگشت" value={REFUND_METHOD_LABEL[request.refundMethod] ?? request.refundMethod} />

            {!request.payable && (
              <p className="flex items-start gap-xs rounded-lg bg-error-container p-sm text-caption text-on-error-container">
                <Icon name="money_off" size={18} className="mt-px shrink-0" />
                پول این سفارش هرگز وصول نشده، پس بازپرداخت روی آن ممکن نیست.
              </p>
            )}

            {request.refundedAt && (
              <p className="text-caption text-on-surface-variant">
                پرداخت در {formatDateTime(request.refundedAt)}
              </p>
            )}
          </Card>

          <Card className="flex flex-col gap-sm p-lg">
            <h2 className="font-headline text-card-title text-primary">مشتری</h2>
            <Link
              href={`/customers/${request.customerId}`}
              className="text-body-md text-secondary transition-colors hover:text-primary"
            >
              {request.customerName}
            </Link>
            <Code className="text-caption">{toPersianDigits(request.customerPhone)}</Code>
          </Card>

          <Card className="flex flex-col gap-sm p-lg">
            <h2 className="font-headline text-card-title text-primary">انبار</h2>
            <p className="flex items-center gap-xs text-body-md text-on-surface-variant">
              <Icon
                name={request.restocked ? 'inventory_2' : 'block'}
                size={20}
                className={request.restocked ? 'text-primary' : undefined}
              />
              {request.restocked
                ? 'کالا به موجودی برگردانده شده است.'
                : 'کالا هنوز به موجودی برنگشته است.'}
            </p>
          </Card>
        </div>
      </div>
    </AdminPage>
  );
}

function Row({ label, value, emphasis }: { label: string; value: string; emphasis?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-md">
      <span className="text-body-md text-on-surface-variant">{label}</span>
      <span
        className={
          emphasis
            ? 'tabular text-body-lg font-semibold text-primary'
            : 'tabular text-body-md text-on-surface'
        }
      >
        {value}
      </span>
    </div>
  );
}
