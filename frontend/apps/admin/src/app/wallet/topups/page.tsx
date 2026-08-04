import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Badge, Card, Code, Icon, formatDateTime, formatPrice } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { FilterBar } from '@/components/FilterBar';
import { WalletTopUpActions } from '@/components/WalletTopUpActions';
import { getWalletTopUps } from '@/lib/api/wallet';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'تأیید شارژ کیف پول' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const statusMeta = {
  pending: { label: 'در انتظار بررسی', tone: 'warning' },
  approved: { label: 'تأیید شده', tone: 'mint' },
  rejected: { label: 'رد شده', tone: 'error' },
} as const;

/**
 * The card-to-card review queue.
 *
 * Owner only, because approving a row here credits spendable balance against a
 * transfer only a person can confirm. Gateway top-ups never appear: those are
 * settled by the gateway's own verification, and a screen that let an operator
 * approve one by hand would be a way to credit a payment nobody took.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRole('owner');
  const params = await searchParams;

  const { items } = await getWalletTopUps({
    q: (first(params.q) ?? '').trim(),
    status: first(params.status) ?? '',
    pageSize: 100,
  });

  return (
    <AdminPage
      title="تأیید شارژ کیف پول"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تأیید شارژ کیف پول' }]}
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی نام، موبایل یا شماره پیگیری..." />
      </Suspense>

      {items.length === 0 ? (
        <Card className="flex flex-col items-center gap-sm p-xl text-center">
          <Icon name="inbox" size={40} className="text-outline" />
          <p className="text-body-md text-on-surface-variant">
            درخواست شارژی برای بررسی وجود ندارد.
          </p>
          <p className="max-w-md text-caption leading-relaxed text-outline">
            اگر شارژ کارت‌به‌کارت در تنظیمات سرور غیرفعال باشد، مشتریان نمی‌توانند
            درخواستی ثبت کنند و این صف همیشه خالی می‌ماند.
          </p>
        </Card>
      ) : (
        <div className="flex flex-col gap-md">
          {items.map((topUp) => {
            const meta = statusMeta[topUp.status];
            return (
              <Card key={topUp.id} className="flex flex-col gap-md p-lg">
                <div className="flex flex-wrap items-start justify-between gap-md">
                  <div className="flex flex-col gap-xs">
                    <span className="text-card-title text-on-surface">{topUp.customerName}</span>
                    <Code className="text-caption">{topUp.customerPhone}</Code>
                  </div>
                  <div className="flex flex-col items-end gap-xs">
                    <span className="tabular text-kpi-mobile font-headline text-primary">
                      {formatPrice(topUp.amount)}
                    </span>
                    <Badge tone={meta.tone}>{meta.label}</Badge>
                  </div>
                </div>

                <dl className="grid gap-sm text-body-md sm:grid-cols-2">
                  <div className="flex items-center justify-between gap-sm sm:justify-start">
                    <dt className="text-on-surface-variant">شماره پیگیری</dt>
                    <dd>
                      <Code className="text-caption">{topUp.trackingNumber ?? '—'}</Code>
                    </dd>
                  </div>
                  <div className="flex items-center justify-between gap-sm sm:justify-start">
                    <dt className="text-on-surface-variant">تاریخ واریز</dt>
                    <dd className="tabular text-caption text-on-surface">
                      {topUp.paidOn ?? '—'}
                    </dd>
                  </div>
                  <div className="flex items-center justify-between gap-sm sm:justify-start">
                    <dt className="text-on-surface-variant">زمان ثبت</dt>
                    <dd className="tabular text-caption text-on-surface">
                      {formatDateTime(topUp.createdAt)}
                    </dd>
                  </div>
                  {topUp.customerNote && (
                    <div className="flex items-start justify-between gap-sm sm:justify-start">
                      <dt className="text-on-surface-variant">توضیح مشتری</dt>
                      <dd className="text-caption text-on-surface">{topUp.customerNote}</dd>
                    </div>
                  )}
                </dl>

                {/*
                  The receipt, which is the one thing the operator is actually
                  being asked to check. It was collected, required, stored and
                  then never shown — the decision below was made on a tracking
                  number and a date alone. The API only stores a receipt it
                  issued itself, so this is a file from our own media root.
                */}
                {topUp.receiptUrl ? (
                  <a
                    href={topUp.receiptUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center gap-sm self-start rounded-lg border border-outline-variant p-sm transition-colors hover:bg-surface-container-low"
                  >
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={topUp.receiptUrl}
                      alt=""
                      className="h-20 w-20 rounded object-cover"
                    />
                    <span className="flex items-center gap-xs text-label-md font-medium text-primary">
                      <Icon name="receipt_long" size={18} />
                      مشاهده رسید واریز
                    </span>
                  </a>
                ) : (
                  <p className="flex items-center gap-xs text-caption text-on-surface-variant">
                    <Icon name="info" size={16} />
                    رسیدی پیوست نشده است.
                  </p>
                )}

                {topUp.status === 'pending' ? (
                  <WalletTopUpActions
                    topUpId={topUp.id}
                    amount={formatPrice(topUp.amount)}
                    customerName={topUp.customerName}
                  />
                ) : (
                  <p className="text-caption text-outline">
                    این درخواست قبلاً بررسی شده و قابل تغییر نیست.
                  </p>
                )}
              </Card>
            );
          })}
        </div>
      )}
    </AdminPage>
  );
}
