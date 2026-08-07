'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Icon, formatPrice } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import { returnNextStatuses, returnStatusMeta } from '@/lib/status';
import type { AdminReturnStatus } from '@/lib/api/types';

interface Props {
  returnId: string;
  status: AdminReturnStatus;
  /** What refunding would pay, worked out by the server from the order. */
  refundEstimate: number;
  /** False when the order was never actually paid for. */
  payable: boolean;
}

/**
 * Moving one return request along.
 *
 * Only forward: the backend refuses a backward transition, so a control that
 * offered one would be a button whose only outcome is an error. The two closed
 * states offer nothing at all.
 *
 * Two things are deliberately in front of the operator before they commit. The
 * refund figure, because it is the number that will actually move and it is
 * derived from the order rather than typed here — there is no amount field, and
 * a body that could name one would be a way to pay a wallet anything. And, at
 * the warehouse step, whether the goods go back on the shelf: the parcel has
 * been out of the shop's hands and may come back damaged, which is a judgement
 * a person has to make rather than something "received" can imply.
 */
export function ReturnDecisionForm({ returnId, status, refundEstimate, payable }: Props) {
  const router = useRouter();
  const [choice, setChoice] = useState<AdminReturnStatus | null>(null);
  const [note, setNote] = useState('');
  const [restock, setRestock] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const options = returnNextStatuses[status];

  if (options.length === 0) {
    return (
      <p className="flex items-center gap-xs text-body-md text-on-surface-variant">
        <Icon name="lock" size={18} />
        این درخواست بسته شده و تغییر دیگری نمی‌پذیرد.
      </p>
    );
  }

  async function decide() {
    if (!choice) return;

    setError(null);
    setPending(true);
    try {
      await postJson('/api/admin/return-decision', {
        id: returnId,
        status: choice,
        note: note.trim(),
        // Only meaningful at the warehouse step; the server ignores it
        // elsewhere, and sending the box's state regardless keeps this from
        // depending on which step the operator happens to be on.
        restock,
      });
      router.refresh();
      setChoice(null);
      setNote('');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت تصمیم انجام نشد.');
    } finally {
      setPending(false);
    }
  }

  const refunding = choice === 'refunded';
  const receiving = choice === 'received';

  return (
    <div className="flex flex-col gap-md">
      <div className="flex flex-wrap gap-sm">
        {options.map((next) => {
          const meta = returnStatusMeta[next];
          return (
            <Button
              key={next}
              size="sm"
              variant={choice === next ? 'primary' : 'outline'}
              icon={meta.icon}
              onClick={() => setChoice(choice === next ? null : next)}
            >
              {meta.label}
            </Button>
          );
        })}
      </div>

      {choice && (
        <div className="flex flex-col gap-md rounded-lg border border-outline-variant bg-surface-container-lowest p-md">
          {refunding && (
            <p
              className={
                payable
                  ? 'flex items-start gap-xs text-body-md text-on-surface'
                  : 'flex items-start gap-xs rounded-lg bg-error-container p-sm text-body-md text-on-error-container'
              }
            >
              <Icon name={payable ? 'payments' : 'error'} size={20} className="mt-px shrink-0" />
              {payable ? (
                <span>
                  مبلغ <strong className="tabular">{formatPrice(refundEstimate)}</strong> به کیف پول
                  مشتری بازگردانده می‌شود. این عدد از قیمت‌های ثبت‌شده‌ی خود سفارش حساب شده است.
                </span>
              ) : (
                <span>
                  پول این سفارش هرگز وصول نشده است، پس چیزی برای بازگرداندن وجود ندارد. سرور این
                  درخواست را رد می‌کند.
                </span>
              )}
            </p>
          )}

          {receiving && (
            <label className="flex items-start gap-sm text-body-md text-on-surface">
              <input
                type="checkbox"
                checked={restock}
                onChange={(event) => setRestock(event.target.checked)}
                className="mt-1 h-5 w-5 shrink-0 accent-primary"
              />
              <span>
                کالا سالم است و به موجودی انبار برگردد.
                <span className="block text-caption text-on-surface-variant">
                  بسته از دست فروشگاه خارج بوده؛ اگر آسیب‌دیده برگشته این تیک را بردارید تا به
                  موجودی اضافه نشود.
                </span>
              </span>
            </label>
          )}

          <label className="flex flex-col gap-xs">
            <span className="text-label-md font-medium text-on-surface-variant">
              یادداشت {choice === 'rejected' ? '(دلیل رد کردن — مشتری آن را می‌بیند)' : '(اختیاری)'}
            </span>
            <textarea
              value={note}
              onChange={(event) => setNote(event.target.value)}
              rows={2}
              maxLength={1000}
              className="rounded-lg border border-outline-variant bg-surface px-md py-sm text-body-md text-on-surface outline-none focus:border-primary"
            />
          </label>

          <div className="flex flex-wrap gap-sm">
            <Button
              size="sm"
              loading={pending}
              disabled={refunding && !payable}
              icon={returnStatusMeta[choice].icon}
              onClick={decide}
            >
              ثبت «{returnStatusMeta[choice].label}»
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setChoice(null)} disabled={pending}>
              انصراف
            </Button>
          </div>
        </div>
      )}

      {error && (
        <p role="alert" className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          {error}
        </p>
      )}
    </div>
  );
}
