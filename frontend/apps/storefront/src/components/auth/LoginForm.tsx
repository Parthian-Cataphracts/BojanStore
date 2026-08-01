'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';
import { safeNextPath } from '@/lib/safe-next';

type Step = 'phone' | 'otp';

/**
 * Phone-first login, matching screen 09 and screen 51 (SMS code verification).
 * Both steps live in one component so the transition stays local.
 *
 * The code is checked server-side by `/api/auth/otp/verify`, which owns the
 * attempt limit and sets the session cookie; nothing about the challenge is
 * held here, so reloading mid-flow cannot be used to reset it.
 */
export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [step, setStep] = useState<Step>('phone');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function requestCode(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(phone);

    if (!/^09\d{9}$/.test(digits)) {
      setError('شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.');
      return;
    }

    setError(null);
    setPending(true);
    try {
      await postJson('/api/auth/otp/request', { phone: digits });
      setStep('otp');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال کد تایید ممکن نشد.');
    } finally {
      setPending(false);
    }
  }

  async function verifyCode(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(code);

    if (digits.length !== 5) {
      setError('کد تایید ۵ رقمی را کامل وارد کنید.');
      return;
    }

    setError(null);
    setPending(true);
    try {
      const result = await postJson<{ isNewUser: boolean }>('/api/auth/otp/verify', {
        code: digits,
      });

      // A first-time number goes to the profile step (screen 52); everyone else
      // returns to whatever the middleware bounced them away from.
      const destination = result.isNewUser
        ? routes.completeProfile
        : safeNextPath(searchParams.get('next'), routes.account);

      router.replace(destination);
      // The session cookie is new — re-render server components against it.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تایید کد ممکن نشد.');
      setPending(false);
    }
  }

  return (
    <Card className="w-full max-w-md p-xl shadow-soft">
      <div className="mb-lg flex flex-col items-center gap-sm text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name={step === 'phone' ? 'person' : 'sms'} size={32} />
        </span>
        <h1 className="font-headline text-display-md text-primary">
          {step === 'phone' ? 'ورود به بوژان' : 'تایید شماره موبایل'}
        </h1>
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          {step === 'phone'
            ? 'شماره موبایل خود را وارد کنید تا کد تایید برایتان ارسال شود.'
            : `کد ۵ رقمی ارسال‌شده به ${toPersianDigits(normalizeDigitsInput(phone))} را وارد کنید.`}
        </p>
      </div>

      {step === 'phone' ? (
        <form onSubmit={requestCode} className="flex flex-col gap-lg">
          <Input
            label="شماره موبایل"
            inputMode="numeric"
            autoComplete="tel"
            placeholder="۰۹۱۲۳۴۵۶۷۸۹"
            icon="smartphone"
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            {...(error ? { error } : null)}
          />

          <Button type="submit" size="lg" fullWidth loading={pending}>
            دریافت کد تایید
          </Button>
        </form>
      ) : (
        <form onSubmit={verifyCode} className="flex flex-col gap-lg">
          <Input
            label="کد تایید"
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={5}
            placeholder="- - - - -"
            icon="pin"
            className="tabular text-center tracking-[0.5em]"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            {...(error ? { error } : null)}
          />

          <Button type="submit" size="lg" fullWidth loading={pending}>
            ورود به حساب
          </Button>

          <button
            type="button"
            onClick={() => {
              setStep('phone');
              setCode('');
              setError(null);
            }}
            className="text-center text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
          >
            ویرایش شماره موبایل
          </button>
        </form>
      )}

      <p className="mt-lg text-center text-caption leading-relaxed text-on-surface-variant">
        با ورود، <Link href={routes.terms} className="text-primary underline">قوانین و مقررات</Link> و{' '}
        <Link href={routes.privacy} className="text-primary underline">حریم خصوصی</Link> بوژان را
        می‌پذیرید.
      </p>
    </Card>
  );
}
