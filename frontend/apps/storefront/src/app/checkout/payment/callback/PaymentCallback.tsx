'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { Icon } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';

/**
 * The gateway sends the shopper back here.
 *
 * Two things arrive back: what was being paid for, and the gateway's reference
 * for the attempt. A wallet top-up is settled here — the API is asked to verify
 * the reference and credit the balance, and it is that call, not this page,
 * that decides. An order is passed to the screens that already exist for it.
 *
 * Calling confirm again is harmless by design, but the guard below still stops
 * React's development double-render from making a second request for nothing.
 */
export function PaymentCallback() {
  const router = useRouter();
  const params = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const settled = useRef(false);

  const target = params.get('order') ?? '';
  const reference = params.get('ref') ?? '';
  const isTopUp = target.startsWith('WALLET-');

  useEffect(() => {
    if (settled.current) return;
    settled.current = true;

    if (!reference) {
      router.replace(routes.paymentFailed);
      return;
    }

    if (!isTopUp) {
      // Orders keep the behaviour they have: the gateway's own outcome decides,
      // and there is no verification endpoint to ask. Left as it was rather
      // than inventing a confirmation this API cannot actually perform.
      router.replace(`${routes.paymentSuccess}?ref=${encodeURIComponent(reference)}`);
      return;
    }

    postJson('/api/account/wallet-topup-confirm', { reference })
      .then(() => router.replace(`${routes.wallet}?topup=ok`))
      .catch((cause: unknown) => {
        setError(cause instanceof Error ? cause.message : 'تأیید پرداخت انجام نشد.');
      });
  }, [isTopUp, reference, router]);

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
