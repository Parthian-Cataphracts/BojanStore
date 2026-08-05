'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, Textarea } from '@bojan/ui';
import { postJson } from '@/lib/submit';

/**
 * Sends one in-app notification to one customer.
 *
 * The panel could only address a whole audience group before this, so telling a
 * single shopper something about their own order meant broadcasting it to
 * everybody or picking up the phone.
 */
export function CustomerNotifyPanel({
  customerId,
  customerName,
}: {
  customerId: string;
  customerName: string;
}) {
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);

    const link = String(data.get('link') ?? '').trim();

    // The API refuses anything that is not a site-relative path, and refusing
    // it here too means the operator is told before the round trip rather than
    // after — a pasted full URL is the obvious mistake, not an attack.
    if (link && !link.startsWith('/')) {
      setError('لینک باید یک مسیر داخلی سایت باشد و با / شروع شود.');
      return;
    }

    setSending(true);
    setError(null);
    try {
      await postJson('/api/admin/customer-notify', {
        customerId,
        title: String(data.get('title') ?? '').trim(),
        body: String(data.get('body') ?? '').trim(),
        link: link || null,
      });
      setSent(true);
      form.reset();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال اعلان انجام نشد.');
    } finally {
      setSending(false);
    }
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <div className="flex items-center gap-sm">
        <Icon name="notifications" size={20} className="text-primary" />
        <h3 className="text-body-lg font-semibold text-primary">ارسال اعلان به این مشتری</h3>
      </div>

      <p className="text-caption leading-relaxed text-on-surface-variant">
        اعلان درون‌برنامه‌ای فقط برای {customerName} ارسال می‌شود و در صفحه‌ی «اعلان‌های من» او
        نمایش داده می‌شود.
      </p>

      <form onSubmit={submit} className="flex flex-col gap-md">
        <Input name="title" label="عنوان" required maxLength={200} />
        <Textarea name="body" label="متن پیام" rows={3} required maxLength={2000} />
        <Input
          name="link"
          label="لینک (اختیاری)"
          placeholder="/account/orders/..."
          className="latin"
          maxLength={500}
        />

        <div className="flex flex-wrap items-center gap-md">
          <Button type="submit" size="sm" loading={sending}>
            ارسال اعلان
          </Button>

          {sent && (
            <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
              <Icon name="check_circle" size={16} />
              اعلان ارسال شد.
            </span>
          )}
          {error && (
            <span role="alert" className="flex items-center gap-xs text-caption text-error">
              <Icon name="error" size={16} />
              {error}
            </span>
          )}
        </div>
      </form>
    </Card>
  );
}
