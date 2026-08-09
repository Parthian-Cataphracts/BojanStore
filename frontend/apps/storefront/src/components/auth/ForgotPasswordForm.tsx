'use client';

import Link from 'next/link';
import { useState, type FormEvent } from 'react';
import { Button, Input } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';
import { AuthCard } from './AuthCard';

/**
 * Asking for a reset link.
 *
 * The confirmation is the same whether or not the address has an account — the
 * API answers identically too. A form that said "no account with that email"
 * would be a way to ask the shop who its customers are, one address at a time.
 */
export function ForgotPasswordForm() {
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const address = email.trim();

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address)) {
      setError('ایمیل معتبر وارد کنید.');
      return;
    }

    setError(null);
    setPending(true);
    try {
      const result = await postJson<{ message?: string }>('/api/auth/password', {
        action: 'forgot',
        email: address,
      });

      setSent(result.message ?? 'اگر این ایمیل ثبت شده باشد، لینک بازیابی ارسال شد.');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'درخواست بازیابی انجام نشد.');
      setPending(false);
    }
  }

  if (sent) {
    return (
      <AuthCard icon="mark_email_read" title="ایمیل را بررسی کنید" caption={sent}>
        <div className="flex flex-col items-center gap-md text-center">
          <p className="text-caption leading-relaxed text-on-surface-variant">
            لینک تا یک ساعت معتبر است. اگر ایمیلی نرسید، پوشه‌ی هرزنامه را هم ببینید.
          </p>

          <Link
            href={`${routes.login}?method=password`}
            className="text-label-md font-label-md text-primary underline underline-offset-4"
          >
            بازگشت به ورود
          </Link>
        </div>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      icon="key"
      title="بازیابی رمز عبور"
      caption="ایمیلی که با آن ثبت‌نام کرده‌اید را وارد کنید تا لینک بازیابی برایتان ارسال شود."
    >
      <form onSubmit={submit} noValidate className="flex flex-col gap-md md:gap-lg">
        <Input
          label="ایمیل"
          type="email"
          autoComplete="email"
          placeholder="name@example.com"
          icon="mail"
          dir="ltr"
          className="latin"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          {...(error ? { error } : null)}
        />

        <Button type="submit" size="lg" fullWidth loading={pending}>
          ارسال لینک بازیابی
        </Button>

        <Link
          href={`${routes.login}?method=password`}
          className="text-center text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
        >
          بازگشت به ورود
        </Link>
      </form>
    </AuthCard>
  );
}
