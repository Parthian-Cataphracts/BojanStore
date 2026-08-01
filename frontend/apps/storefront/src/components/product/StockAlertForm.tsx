'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Checkbox, Icon, Input, normalizeDigitsInput } from '@bojan/ui';
import { OptionGroup } from '@/components/checkout/OptionGroup';
import { formPayload, postJson } from '@/lib/api/submit';

type Channel = 'sms' | 'email';

/** Screen 87 — Choose how to be told when stock returns. */
export function StockAlertForm({
  productSlug,
  productTitle,
}: {
  productSlug: string;
  productTitle: string;
}) {
  const [channel, setChannel] = useState<Channel>('sms');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [done, setDone] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const data = new FormData(event.currentTarget);

    if (channel === 'sms') {
      const phone = normalizeDigitsInput(String(data.get('phone') ?? ''));
      if (!/^09\d{9}$/.test(phone)) {
        setError('شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.');
        return;
      }
    } else {
      const email = String(data.get('email') ?? '').trim();
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        setError('ایمیل معتبر نیست.');
        return;
      }
    }

    setError(null);
    setSaving(true);
    try {
      await postJson('/api/account/stock-alert', { ...formPayload(form), productSlug });
      setDone(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت درخواست انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  if (done) {
    return (
      <Card className="flex flex-col items-center gap-md p-xl text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="notifications_active" size={30} />
        </span>
        <h2 className="font-headline text-card-title text-primary">درخواست شما ثبت شد</h2>
        <p className="max-w-md text-body-md leading-loose text-on-surface-variant">
          به محض موجود شدن «{productTitle}» به شما اطلاع می‌دهیم.
        </p>
      </Card>
    );
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-card-title text-primary">روش اطلاع‌رسانی</h2>
        <OptionGroup
          name="channel"
          columns={2}
          options={[
            { id: 'sms', title: 'پیامک', description: 'سریع‌ترین راه اطلاع‌رسانی', icon: 'sms' },
            { id: 'email', title: 'ایمیل', description: 'همراه با جزئیات محصول', icon: 'mail' },
          ]}
          onChange={(id) => {
            setChannel(id as Channel);
            setError(null);
          }}
        />
      </section>

      <Card className="flex flex-col gap-lg p-lg">
        {channel === 'sms' ? (
          <Input
            name="phone"
            type="tel"
            inputMode="numeric"
            label="شماره موبایل"
            placeholder="۰۹۱۲۳۴۵۶۷۸۹"
            icon="smartphone"
            required
            {...(error ? { error } : null)}
          />
        ) : (
          <Input
            name="email"
            type="email"
            label="ایمیل"
            placeholder="example@domain.com"
            icon="mail"
            required
            {...(error ? { error } : null)}
          />
        )}

        <Checkbox
          name="offers"
          label="پیشنهادهای مشابه این محصول را هم برایم بفرستید."
        />
      </Card>

      <Button type="submit" size="lg" loading={saving} className="self-start px-xl">
        ثبت درخواست اطلاع‌رسانی
      </Button>
    </form>
  );
}
