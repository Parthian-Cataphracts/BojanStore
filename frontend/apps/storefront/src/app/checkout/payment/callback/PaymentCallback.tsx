'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { Icon } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';

/** What the API answers when it has resolved the reference. */
interface CallbackResult {
  kind: 'order' | 'wallet';
  orderNumber?: string | null;
  reference?: string | null;
  paid: boolean;
}

/**
 * The gateway sends the shopper back here.
 *
 * Only one thing arrives that is worth anything: the gateway's reference for
 * the attempt — `Authority` in ZarinPal's case. Everything else in the query
 * string, including the `Status=OK` that says the payment succeeded, was
 * written by whoever is holding the browser, so none of it is passed on. The
 * API is asked instead: it looks the reference up against its own records,
 * finds out whether it belongs to an order or a wallet top-up, asks the gateway
 * whether the money actually arrived, and answers with the outcome. This page
 * decides nothing.
 *
 * `Status=NOK` is read for one purpose only — going straight to the failure
 * screen rather than making the shopper wait through a verification whose
 * answer is already known. It can only make the page give up early, never
 * settle anything.
 *
 * Calling the API again is harmless by design, but the guard below still stops
 * React's development double-render from making a second request for nothing.
 */
export function PaymentCallback() {
  const router = useRouter();
  const params = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const settled = useRef(false);

  // Both spellings. ZarinPal returns `Authority` and `Status`; the built-in
  // stub sends the same names, and `ref`/`order` are what an older build used.
  const reference = params.get('Authority') ?? params.get('ref') ?? '';
  const cancelled = (params.get('Status') ?? '').toUpperCase() === 'NOK';

  useEffect(() => {
    if (settled.current) return;
    settled.current = true;

    if (!reference || cancelled) {
      router.replace(routes.paymentFailed);
      return;
    }

    postJson<CallbackResult>('/api/account/payment-callback', { reference })
      .then((result) => {
        if (result.kind === 'wallet') {
          router.replace(`${routes.wallet}?topup=${result.paid ? 'ok' : 'failed'}`);
          return;
        }

        if (!result.paid) {
          // The order is real and still awaiting payment — it is not cancelled
          // by a payment that did not complete, and the shopper can pay it
          // again from their orders. The failure screen says so.
          router.replace(
            result.orderNumber
              ? `${routes.paymentFailed}?order=${encodeURIComponent(result.orderNumber)}`
              : routes.paymentFailed,
          );
          return;
        }

        router.replace(
          result.orderNumber
            ? `${routes.paymentSuccess}?order=${encodeURIComponent(result.orderNumber)}`
            : `${routes.paymentSuccess}?ref=${encodeURIComponent(reference)}`,
        );
      })
      .catch((cause: unknown) => {
        setError(cause instanceof Error ? cause.message : 'تأیید پرداخت انجام نشد.');
      });
  }, [cancelled, reference, router]);

  if (error) {
    return (
      <div className="flex flex-col items-center gap-md py-xl text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-error-container text-on-error-container">
          <Icon name="error" size={32} />
        </span>
        <h1 className="font-headline text-display-md text-primary">پرداخت تأیید نشد</h1>
        <p role="alert" className="max-w-md text-body-md leading-relaxed text-on-surface-variant">
          {error}
        </p>
        <p className="text-caption text-outline">
          اگر مبلغ از حساب شما کسر شده، تا ۷۲ ساعت آینده به‌صورت خودکار بازگردانده می‌شود.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center gap-md py-xl text-center">
      <Icon name="progress_activity" size={40} className="animate-spin text-primary" />
      <p className="text-body-md text-on-surface-variant">در حال تأیید پرداخت…</p>
      <p className="text-caption text-outline">این صفحه را نبندید.</p>
    </div>
  );
}
