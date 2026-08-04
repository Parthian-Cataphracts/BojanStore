'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Icon } from '@bojan/ui';
import { postJson } from '@/lib/submit';

interface Props {
  topUpId: string;
  amount: string;
  customerName: string;
}

/**
 * Approve or reject one card-to-card transfer.
 *
 * Approving credits spendable balance against money the operator says they have
 * seen arrive, so it asks once before doing it and names the amount and the
 * customer in the question — the two things that make it the wrong row if it is
 * the wrong row. Rejecting takes a reason, which the customer is shown.
 */
export function WalletTopUpActions({ topUpId, amount, customerName }: Props) {
  const router = useRouter();
  const [mode, setMode] = useState<'idle' | 'approve' | 'reject'>('idle');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function decide(approve: boolean) {
    setError(null);
    setPending(true);
    try {
      await postJson('/api/admin/wallet-topup-decision', {
        id: topUpId,
        approve,
        note: note.trim(),
      });
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت تصمیم انجام نشد.');
      setPending(false);
    }
  }

  if (error) {
    return (
      <p role="alert" className="text-caption text-error">
        {error}
      </p>
    );
  }

  if (mode === 'idle') {
    return (
      <div className="flex flex-wrap gap-sm">
        <Button size="sm" icon="check" onClick={() => setMode('approve')}>
          تأیید واریز
        </Button>
        <Button size="sm" variant="outline" icon="close" onClick={() => setMode('reject')}>
          رد کردن
        </Button>
      </div>
    );
  }

  const approving = mode === 'approve';

  return (
    <div className="flex flex-col gap-sm rounded-lg border border-outline-variant p-md">
      <p className="flex items-start gap-xs text-body-md text-on-surface">
        <Icon
          name={approving ? 'account_balance_wallet' : 'block'}
          size={20}
          className={approving ? 'mt-px shrink-0 text-primary' : 'mt-px shrink-0 text-error'}
        />
        {approving
          ? `مبلغ ${amount} به کیف پول ${customerName} اضافه شود؟ این کار برگشت‌پذیر نیست.`
          : `درخواست ${customerName} رد شود؟`}
      </p>

      <label className="flex flex-col gap-xs">
        <span className="text-caption text-on-surface-variant">
          {approving ? 'یادداشت (اختیاری)' : 'دلیل رد — به مشتری نمایش داده می‌شود'}
        </span>
        <input
          value={note}
          onChange={(event) => setNote(event.target.value)}
          className="h-11 rounded-lg border border-outline-variant bg-surface-container-lowest px-md text-body-md text-on-surface placeholder:text-outline focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30"
          placeholder={approving ? 'مثال: مطابق صورت‌حساب' : 'مثال: واریزی یافت نشد'}
        />
      </label>

      <div className="flex flex-wrap gap-sm">
        <Button
          size="sm"
          variant={approving ? 'primary' : 'danger'}
          loading={pending}
          disabled={!approving && note.trim().length === 0}
          onClick={() => decide(approving)}
        >
          {approving ? 'تأیید و افزودن اعتبار' : 'رد درخواست'}
        </Button>
        <Button size="sm" variant="ghost" onClick={() => setMode('idle')} disabled={pending}>
          انصراف
        </Button>
      </div>
    </div>
  );
}
