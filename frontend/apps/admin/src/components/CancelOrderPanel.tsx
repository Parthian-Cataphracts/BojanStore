'use client';

import { useState } from 'react';
import { Button, Card, FormStatus, Icon, Textarea, formatPrice } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import type { AdminOrderStatus } from '@/lib/types';

/**
 * Screen 95 — cancelling an order, beside the status control rather than
 * inside it.
 *
 * Cancelling is not another step on the fulfilment path: it puts stock back and
 * moves money, and what it does depends on how far the order got. Making it a
 * value the status list could pick would have hidden all of that behind a radio
 * button, which is how an operator ends up refunding an order they meant to
 * mark shipped.
 *
 * The consequences are stated before the operator confirms, not after. The
 * server decides them either way — this panel derives the same answer from the
 * same rules only so the button can say what pressing it will do.
 */

/** Cancellable at all. A delivered order is returned instead; the rest are terminal. */
const cancellable: AdminOrderStatus[] = ['pending', 'processing', 'packed', 'shipped'];

/** The penalty starts at the warehouse — see `OrderCancellation` on the server. */
const penalised: AdminOrderStatus[] = ['packed', 'shipped'];

/** Dispatched goods are with a carrier, so their return to stock is recorded by hand. */
const autoRestocked: AdminOrderStatus[] = ['pending', 'processing', 'packed'];

interface CancellationResult {
  refunded: number;
  penalty: number;
  restocked: boolean;
  manualGatewayRefund: number;
}

export function CancelOrderPanel({
  orderId,
  status,
  penaltyPercent,
}: {
  orderId: string;
  status: AdminOrderStatus;
  /** The configured percentage, so the warning quotes the real figure rather than a placeholder. */
  penaltyPercent: number;
}) {
  const [confirming, setConfirming] = useState(false);
  const [reason, setReason] = useState('');
  const [chargePenalty, setChargePenalty] = useState(true);
  const [saving, setSaving] = useState(false);
  const [result, setResult] = useState<CancellationResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!cancellable.includes(status)) {
    return null;
  }

  const willPenalise = chargePenalty && penalised.includes(status) && penaltyPercent > 0;
  const willRestock = autoRestocked.includes(status);

  async function cancel() {
    setSaving(true);
    setError(null);
    try {
      const outcome = await postJson<CancellationResult>('/api/admin/order-cancel', {
        id: orderId,
        reason: reason.trim() || undefined,
        chargePenalty,
      });
      setResult(outcome);
      setConfirming(false);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'لغو سفارش انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  if (result) {
    return (
      <Card className="flex flex-col gap-sm p-lg">
        <h3 className="flex items-center gap-xs font-headline text-card-title text-primary">
          <Icon name="check_circle" size={20} />
          سفارش لغو شد
        </h3>

        <p className="text-body-md text-on-surface">
          مبلغ {formatPrice(result.refunded)} به کیف پول مشتری بازگشت.
          {result.penalty > 0 && ` (${formatPrice(result.penalty)} بابت جریمه لغو کسر شد.)`}
        </p>

        {/* The two things that may still need a person. */}
        {!result.restocked && (
          <p className="flex items-start gap-xs rounded-lg bg-warning-container px-md py-sm text-caption text-on-warning-container">
            <Icon name="warehouse" size={16} className="mt-px shrink-0" />
            این سفارش ارسال شده بود؛ موجودی به‌صورت خودکار برنگشت. پس از دریافت مرسوله، از صفحه
            «موجودی و انبار» ورود کالا را ثبت کنید.
          </p>
        )}

        {result.manualGatewayRefund > 0 && (
          <p className="flex items-start gap-xs rounded-lg bg-warning-container px-md py-sm text-caption text-on-warning-container">
            <Icon name="credit_card" size={16} className="mt-px shrink-0" />
            مبلغ {formatPrice(result.manualGatewayRefund)} از طریق درگاه پرداخت دریافت شده بود و باید
            دستی به مشتری بازگردانده شود.
          </p>
        )}
      </Card>
    );
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <h3 className="font-headline text-card-title text-primary">لغو سفارش</h3>

      {!confirming ? (
        <>
          <p className="text-caption leading-relaxed text-on-surface-variant">
            {willRestock
              ? 'موجودی کالاها به انبار بازمی‌گردد و مبلغ پرداخت‌شده از کیف پول به مشتری برمی‌گردد.'
              : 'مبلغ پرداخت‌شده از کیف پول به مشتری برمی‌گردد. چون سفارش ارسال شده، موجودی باید پس از بازگشت مرسوله دستی ثبت شود.'}
          </p>

          <Button variant="danger" icon="cancel" onClick={() => setConfirming(true)} fullWidth>
            لغو سفارش
          </Button>
        </>
      ) : (
        <>
          {penalised.includes(status) && (
            <p className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-caption text-on-error-container">
              <Icon name="info" size={16} className="mt-px shrink-0" />
              {penaltyPercent > 0
                ? `این سفارش به مرحله انبار رسیده است؛ در صورت لغو به‌درخواست مشتری، ${penaltyPercent}٪ جریمه از مبلغ بازگشتی کسر می‌شود.`
                : 'این سفارش به مرحله انبار رسیده است. درصد جریمه لغو تنظیم نشده، پس چیزی کسر نمی‌شود.'}
            </p>
          )}

          <Textarea
            label="دلیل لغو"
            placeholder="دلیل لغو سفارش..."
            rows={2}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />

          {/*
            Cleared when the shop is at fault. Charging someone a penalty for a
            decision that was not theirs is not a penalty, it is a deduction.
          */}
          <label className="flex items-start gap-sm text-body-md text-on-surface">
            <input
              type="checkbox"
              checked={chargePenalty}
              onChange={(event) => setChargePenalty(event.target.checked)}
              className="mt-1 h-4 w-4 accent-primary"
            />
            <span className="flex flex-col gap-xs">
              لغو به‌درخواست مشتری بوده است
              <span className="text-caption text-on-surface-variant">
                اگر لغو به‌دلیل اشتباه یا ناتوانی فروشگاه است، این گزینه را بردارید تا جریمه‌ای کسر نشود.
              </span>
            </span>
          </label>

          <p className="text-caption text-on-surface-variant">
            {willPenalise
              ? `مبلغ بازگشتی پس از کسر ${penaltyPercent}٪ جریمه محاسبه می‌شود.`
              : 'کل مبلغ پرداخت‌شده از کیف پول بازمی‌گردد.'}
          </p>

          <div className="flex flex-wrap gap-sm">
            <Button variant="danger" loading={saving} onClick={cancel} className="px-xl">
              تایید و لغو سفارش
            </Button>
            <Button variant="ghost" onClick={() => setConfirming(false)} className="px-lg">
              انصراف
            </Button>
          </div>
        </>
      )}

      <FormStatus error={error} />
    </Card>
  );
}
