'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Card, Icon, Sheet, Textarea, formatPrice } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import type { OrderStatus } from '@/lib/api/types';

/**
 * Screen 13 — cancelling an order the shopper no longer wants.
 *
 * Only offered while there is something to cancel: once the order is delivered
 * the way back is a return, which the button beside this one already offers.
 *
 * The warning is hedged on purpose. The penalty starts when the order reaches
 * the warehouse, and this screen cannot tell whether it has: the customer-facing
 * `OrderStatus` has no `packed` — the API folds it into `processing` (see
 * `OrderStatus` on the server) — so a confident "this will cost you" would be
 * wrong for every order that is merely confirmed. What it can do is say the
 * rule, and then say what actually happened: the API returns the real refund
 * and the real penalty, and those are shown rather than guessed at.
 */

/** Cancellable at all. The API decides again; this is only what to show. */
const cancellable: OrderStatus[] = ['pending', 'processing', 'shipped'];

/** States that may already be past the warehouse — see the note above. */
const mayBePenalised: OrderStatus[] = ['processing', 'shipped'];

interface CancellationResult {
  refunded: number;
  penalty: number;
  manualGatewayRefund: number;
}

export function CancelOrderButton({ orderId, status }: { orderId: string; status: OrderStatus }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [result, setResult] = useState<CancellationResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!cancellable.includes(status)) return null;

  async function cancel() {
    setSaving(true);
    setError(null);
    try {
      setResult(
        await postJson<CancellationResult>('/api/account/order-cancel', {
          orderId,
          reason: reason.trim() || undefined,
        }),
      );
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'لغو سفارش انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  function done() {
    setOpen(false);
    setResult(null);
    // The status, the timeline and the wallet all changed, so the screen is
    // re-read rather than patched here.
    router.refresh();
  }

  return (
    <>
      <Button
        variant="outline"
        size="lg"
        fullWidth
        icon="cancel"
        className="gap-sm"
        onClick={() => setOpen(true)}
      >
        لغو سفارش
      </Button>

      <Sheet open={open} onClose={result ? done : () => setOpen(false)} title="لغو سفارش">
        {result ? (
          <div className="flex flex-col gap-md p-lg">
            <p className="flex items-start gap-xs text-body-md leading-relaxed text-on-surface">
              <Icon name="check_circle" size={20} className="mt-px shrink-0 text-primary" />
              سفارش لغو شد.
            </p>

            {result.refunded > 0 && (
              <Card className="flex flex-col gap-xs p-lg">
                <span className="text-caption text-on-surface-variant">بازگشت به کیف پول</span>
                <span className="tabular text-body-lg font-semibold text-primary">
                  {formatPrice(result.refunded)}
                </span>
                {result.penalty > 0 && (
                  <span className="text-caption text-on-surface-variant">
                    {formatPrice(result.penalty)} بابت جریمه لغو کسر شد.
                  </span>
                )}
              </Card>
            )}

            {result.manualGatewayRefund > 0 && (
              <p className="flex items-start gap-xs rounded-lg bg-surface-container-low px-md py-sm text-caption leading-relaxed text-on-surface-variant">
                <Icon name="credit_card" size={16} className="mt-px shrink-0" />
                مبلغ {formatPrice(result.manualGatewayRefund)} از طریق درگاه بانکی پرداخت شده بود و
                توسط پشتیبانی به شما بازگردانده می‌شود.
              </p>
            )}

            <Button size="lg" fullWidth onClick={done}>
              متوجه شدم
            </Button>
          </div>
        ) : (
          <div className="flex flex-col gap-md p-lg">
            <p className="text-body-md leading-relaxed text-on-surface-variant">
              پس از لغو، مبلغی که از کیف پول پرداخت کرده‌اید به کیف پول شما بازمی‌گردد.
            </p>

            {mayBePenalised.includes(status) && (
              <Card className="flex items-start gap-xs bg-error-container p-md text-body-md text-on-error-container">
                <Icon name="info" size={20} className="mt-px shrink-0" />
                <span className="leading-relaxed">
                  اگر سفارش وارد مرحله آماده‌سازی در انبار شده باشد، طبق قوانین فروشگاه درصدی از مبلغ
                  بابت جریمه لغو کسر می‌شود. مبلغ دقیق پس از تایید نمایش داده می‌شود.
                </span>
              </Card>
            )}

            <Textarea
              label="دلیل لغو (اختیاری)"
              placeholder="اگر مایل هستید، دلیل لغو را بنویسید."
              rows={3}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />

            {error && (
              <p role="alert" className="flex items-center gap-xs text-caption text-error">
                <Icon name="error" size={16} />
                {error}
              </p>
            )}

            <div className="flex flex-col gap-sm sm:flex-row">
              <Button variant="danger" size="lg" fullWidth loading={saving} onClick={cancel}>
                تایید لغو سفارش
              </Button>
              <Button variant="ghost" size="lg" fullWidth onClick={() => setOpen(false)}>
                انصراف
              </Button>
            </div>
          </div>
        )}
      </Sheet>
    </>
  );
}
