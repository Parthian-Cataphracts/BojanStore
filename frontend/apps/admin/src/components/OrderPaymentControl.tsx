'use client';

import { useState } from 'react';
import { Button, Card, Icon, Input, cn } from '@bojan/ui';
import { formatDate } from '@bojan/ui';
import { postJson } from '@/lib/submit';

/** The four states an order's money can be in — `Order.PaymentStatus`. */
export type OrderPaymentStatus = 'awaiting-payment' | 'paid' | 'refunded' | 'failed';

const meta: Record<OrderPaymentStatus, { label: string; icon: string; tone: string }> = {
  'awaiting-payment': {
    label: 'در انتظار پرداخت',
    icon: 'schedule',
    tone: 'text-on-surface-variant',
  },
  paid: { label: 'پرداخت شده', icon: 'check_circle', tone: 'text-primary' },
  refunded: { label: 'بازپرداخت شده', icon: 'undo', tone: 'text-secondary' },
  failed: { label: 'پرداخت ناموفق', icon: 'error', tone: 'text-error' },
};

/**
 * Whether the money for this order arrived, and the one control that records
 * it.
 *
 * The order's payment state existed everywhere except here: it had a column, a
 * migration, a rule stopping an unpaid order from shipping, and domain tests —
 * and no screen read it and no button wrote it. So an operator could not see
 * whether an order had been paid for, and a cash-on-delivery order stayed
 * "awaiting payment" forever after the courier handed the money over, which is
 * the state the returns queue reads to decide whether a refund is owed.
 *
 * Only shown as an action while the order is still awaiting payment. The three
 * other states are ends: `MarkPaid` acts on nothing else, and offering a button
 * that the API will refuse is worse than not offering one.
 *
 * An online order settles itself — the shopper's return from the gateway is
 * verified against the gateway, and the reconciliation worker catches the ones
 * who never came back. This is for the money that arrives some other way: cash
 * at the door, a transfer, a correction.
 */
export function OrderPaymentControl({
  orderId,
  status,
  paidAt,
  reference,
  method,
}: {
  orderId: string;
  status: OrderPaymentStatus;
  paidAt?: string | null;
  reference?: string | null;
  method: string;
}) {
  const [current, setCurrent] = useState(status);
  const [settledReference, setSettledReference] = useState(reference ?? '');
  const [input, setInput] = useState('');
  const [saving, setSaving] = useState<'paid' | 'failed' | null>(null);
  const [error, setError] = useState<string | null>(null);

  const shown = meta[current] ?? meta['awaiting-payment'];

  async function settle(paid: boolean) {
    setSaving(paid ? 'paid' : 'failed');
    setError(null);

    try {
      await postJson('/api/admin/order-payment', {
        id: orderId,
        paid,
        reference: input.trim() || undefined,
      });

      setCurrent(paid ? 'paid' : 'failed');
      setSettledReference(input.trim());
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت پرداخت انجام نشد.');
    } finally {
      setSaving(null);
    }
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <h3 className="font-headline text-card-title text-primary">وضعیت پرداخت</h3>

      <div className={cn('flex items-center gap-sm', shown.tone)}>
        <Icon name={shown.icon} size={20} />
        <span className="text-body-md font-medium">{shown.label}</span>
      </div>

      <dl className="flex flex-col gap-sm">
        <div className="flex items-center justify-between gap-md border-b border-paper-border pb-sm">
          <dt className="text-caption text-on-surface-variant">روش پرداخت</dt>
          <dd className="text-caption text-on-surface">{method}</dd>
        </div>

        {paidAt && (
          <div className="flex items-center justify-between gap-md border-b border-paper-border pb-sm">
            <dt className="text-caption text-on-surface-variant">زمان تسویه</dt>
            <dd className="tabular text-caption text-on-surface">{formatDate(paidAt, 'long')}</dd>
          </div>
        )}

        {settledReference && (
          <div className="flex items-center justify-between gap-md">
            <dt className="text-caption text-on-surface-variant">کد پیگیری</dt>
            <dd className="latin tabular text-caption text-on-surface">{settledReference}</dd>
          </div>
        )}
      </dl>

      {current === 'awaiting-payment' ? (
        <>
          <Input
            label="کد پیگیری (اختیاری)"
            value={input}
            onChange={(event) => setInput(event.target.value)}
            placeholder="شماره پیگیری واریز یا رسید"
            className="latin"
          />

          <div className="flex flex-wrap gap-sm">
            <Button
              type="button"
              size="sm"
              loading={saving === 'paid'}
              disabled={saving !== null}
              onClick={() => settle(true)}
            >
              ثبت دریافت وجه
            </Button>

            <Button
              type="button"
              size="sm"
              variant="outline"
              loading={saving === 'failed'}
              disabled={saving !== null}
              onClick={() => settle(false)}
            >
              ثبت پرداخت ناموفق
            </Button>
          </div>

          <p className="text-caption leading-relaxed text-on-surface-variant">
            سفارش‌های پرداخت‌اینترنتی خودشان پس از بازگشت خریدار از درگاه تسویه می‌شوند. این
            دکمه‌ها برای پولی است که از راه دیگری می‌رسد — پرداخت در محل، کارت‌به‌کارت، یا
            اصلاح یک اشتباه.
          </p>
        </>
      ) : (
        <p className="text-caption leading-relaxed text-on-surface-variant">
          وضعیت پرداخت این سفارش تعیین شده و دیگر از این صفحه تغییر نمی‌کند. بازپرداخت از
          مسیر لغو سفارش یا مرجوعی ثبت می‌شود.
        </p>
      )}

      {error && (
        <span role="alert" className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          {error}
        </span>
      )}
    </Card>
  );
}
