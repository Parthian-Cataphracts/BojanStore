'use client';

import { useState } from 'react';
import { Button, Card, FormStatus, Icon, Textarea, cn } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import { orderStatusMeta } from '@/lib/status';
import type { AdminOrderStatus } from '@/lib/types';

/** The fulfilment path an order normally walks. */
const flow: AdminOrderStatus[] = ['pending', 'processing', 'packed', 'shipped', 'delivered'];

/** Screen 95 — Change order status. */
export function OrderStatusControl({
  orderId,
  current,
}: {
  orderId: string;
  current: AdminOrderStatus;
}) {
  const [status, setStatus] = useState<AdminOrderStatus>(current);
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const currentIndex = flow.indexOf(status);

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await postJson('/api/admin/order-status', { id: orderId, status, note: note.trim() || undefined });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تغییر وضعیت انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <h3 className="font-headline text-card-title text-primary">تغییر وضعیت سفارش</h3>

      <div className="flex flex-col gap-sm">
        {flow.map((step, index) => {
          const meta = orderStatusMeta[step];
          const done = currentIndex >= 0 && index < currentIndex;
          const active = step === status;

          return (
            <button
              key={step}
              type="button"
              onClick={() => setStatus(step)}
              className={cn(
                'flex items-center gap-sm rounded-lg border p-sm text-start transition-colors',
                active
                  ? 'border-primary bg-soft-mint/40'
                  : 'border-outline-variant hover:bg-surface-container-low',
              )}
            >
              <Icon
                name={done ? 'check_circle' : active ? 'radio_button_checked' : 'radio_button_unchecked'}
                size={20}
                className={done || active ? 'text-primary' : 'text-outline-variant'}
              />
              <span className="text-body-md text-on-surface">{meta.label}</span>
            </button>
          );
        })}
      </div>

      {/*
        Terminal states are set explicitly, never by walking the flow.

        «لغو شده» is deliberately not here. Cancelling puts stock back and
        refunds the wallet less any penalty, and none of that happens on a plain
        status change — offering it as one more radio button meant an operator
        could cancel an order and leave the customer's money and the shop's
        stock exactly where they were. It has its own control (see
        `CancelOrderPanel`), which says what it is about to do before it does it.
      */}
      <div className="flex flex-wrap gap-sm border-t border-paper-border pt-md">
        {(['returned'] as AdminOrderStatus[]).map((step) => {
          const meta = orderStatusMeta[step];
          return (
            <button
              key={step}
              type="button"
              onClick={() => setStatus(step)}
              className={cn(
                'rounded-full border px-md py-xs text-caption font-medium transition-colors',
                status === step
                  ? 'border-error bg-error-container text-on-error-container'
                  : 'border-outline-variant text-on-surface-variant hover:bg-surface-container-low',
              )}
            >
              {meta.label}
            </button>
          );
        })}
      </div>

      <Textarea
        label="یادداشت داخلی"
        placeholder="دلیل تغییر وضعیت..."
        rows={2}
        value={note}
        onChange={(event) => setNote(event.target.value)}
      />

      <Button loading={saving} onClick={save} fullWidth>
        ثبت تغییر وضعیت
      </Button>

      <FormStatus error={error} />
      <FormStatus ok={saved ? 'وضعیت سفارش به «{orderStatusMeta[status].label}» تغییر کرد.' : null} />

      <p className="text-caption text-outline">
        تغییر وضعیت برای مشتری پیامک می‌شود.{' '}
        <span className="latin-inline">#{orderId}</span>
      </p>
    </Card>
  );
}
