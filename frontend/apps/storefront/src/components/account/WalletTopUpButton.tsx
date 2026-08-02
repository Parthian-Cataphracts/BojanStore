'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Icon, Input, buttonClasses } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';

const PRESETS = [100_000, 300_000, 500_000, 1_000_000];

/**
 * Screen 58's "افزایش اعتبار" — now that `POST /me/wallet/topup` exists, the
 * amount picker it opens onto and the round trip that follows.
 */
export function WalletTopUpButton() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [amount, setAmount] = useState<number | ''>('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!amount || amount < 1000) {
      setError('مبلغ را وارد کنید.');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await postJson('/api/account/wallet-topup', { amount });
      setOpen(false);
      setAmount('');
      // The balance and history both live on the server component above this
      // one — a refresh is what picks up the credit this call just recorded.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'افزایش اعتبار انجام نشد.');
    } finally {
      setSubmitting(false);
    }
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={buttonClasses({ fullWidth: true, className: 'gap-sm' })}
      >
        <Icon name="add" size={20} />
        افزایش اعتبار
      </button>
    );
  }

  return (
    <form onSubmit={submit} className="flex w-full flex-col gap-sm">
      <div className="flex flex-wrap gap-xs">
        {PRESETS.map((preset) => (
          <button
            key={preset}
            type="button"
            onClick={() => setAmount(preset)}
            className={buttonClasses({
              variant: amount === preset ? 'primary' : 'outline',
              size: 'sm',
            })}
          >
            {preset.toLocaleString('fa-IR')}
          </button>
        ))}
      </div>

      <Input
        type="number"
        inputMode="numeric"
        min={1000}
        placeholder="مبلغ به تومان"
        value={amount}
        onChange={(event) => setAmount(event.target.value ? Number(event.target.value) : '')}
      />

      {error && <p className="text-caption text-error">{error}</p>}

      <div className="flex gap-sm">
        <Button type="submit" disabled={submitting} fullWidth>
          {submitting ? 'در حال پرداخت…' : 'پرداخت و افزایش اعتبار'}
        </Button>
        <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={submitting}>
          انصراف
        </Button>
      </div>
    </form>
  );
}
