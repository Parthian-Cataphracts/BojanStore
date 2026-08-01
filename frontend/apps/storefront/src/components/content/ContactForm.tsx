'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, Textarea, normalizeDigitsInput } from '@bojan/ui';
import { formPayload, postJson } from '@/lib/api/submit';

type Errors = Partial<Record<'name' | 'phone' | 'subject' | 'message' | 'form', string>>;

/** The message form on screen 18. */
export function ContactForm() {
  const [errors, setErrors] = useState<Errors>({});
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const name = String(data.get('name') ?? '').trim();
    const phone = normalizeDigitsInput(String(data.get('phone') ?? ''));
    const subject = String(data.get('subject') ?? '').trim();
    const message = String(data.get('message') ?? '').trim();

    if (name.length < 3) next.name = 'نام و نام خانوادگی را کامل وارد کنید.';
    if (!/^09\d{9}$/.test(phone)) next.phone = 'شماره تماس باید ۱۱ رقم و با ۰۹ شروع شود.';
    if (!subject) next.subject = 'موضوع پیام را وارد کنید.';
    if (message.length < 10) next.message = 'متن پیام را کامل‌تر بنویسید.';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSending(true);
    try {
      await postJson('/api/account/contact-message', formPayload(form));
      setSent(true);
      form.reset();
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ارسال پیام انجام نشد.' });
    } finally {
      setSending(false);
    }
  }

  if (sent) {
    return (
      <Card className="flex flex-col items-center gap-md p-xl text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="mark_email_read" size={30} />
        </span>
        <h2 className="font-headline text-display-md text-primary">پیام شما ارسال شد</h2>
        <p className="max-w-md text-body-md leading-loose text-on-surface-variant">
          کارشناسان بوژان در کمتر از ۲۴ ساعت کاری با شما تماس می‌گیرند.
        </p>
        <Button variant="outline" onClick={() => setSent(false)}>
          ارسال پیام دیگر
        </Button>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col gap-lg p-lg">
      <h2 className="font-headline text-display-md text-primary">ارسال پیام</h2>

      <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
        <div className="grid gap-lg md:grid-cols-2">
          <Input
            name="name"
            label="نام و نام خانوادگی"
            placeholder="مثال: علی رضایی"
            required
            {...(errors.name ? { error: errors.name } : null)}
          />
          <Input
            name="phone"
            type="tel"
            inputMode="numeric"
            label="شماره تماس"
            placeholder="۰۹۱۲۳۴۵۶۷۸۹"
            required
            {...(errors.phone ? { error: errors.phone } : null)}
          />
        </div>

        <Input
          name="subject"
          label="موضوع پیام"
          placeholder="موضوع خود را وارد کنید"
          required
          {...(errors.subject ? { error: errors.subject } : null)}
        />

        <Textarea
          name="message"
          label="متن پیام"
          placeholder="پیام خود را بنویسید..."
          rows={6}
          required
          {...(errors.message ? { error: errors.message } : null)}
        />

        {errors.form && (
          <p
            role="alert"
            className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
          >
            <Icon name="error" size={20} className="mt-px shrink-0" />
            {errors.form}
          </p>
        )}

        <Button type="submit" size="lg" fullWidth loading={sending}>
          ارسال پیام
        </Button>
      </form>
    </Card>
  );
}
