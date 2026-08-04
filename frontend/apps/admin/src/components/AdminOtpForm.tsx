'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { safeNextPath } from '@/lib/safe-next';
import { postJson } from '@/lib/submit';

type Step = 'phone' | 'code';

/**
 * One-time-code sign-in, reached from the link screen 91 draws.
 *
 * The design gives the entry point but not this step, so the card reuses screen
 * 91's own vocabulary — same shell, same rule, same field and button
 * treatments — rather than introducing anything new.
 */
export function AdminOtpForm() {
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
      await postJson('/api/admin-auth/otp', { action: 'request', phone: digits });
      setStep('code');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال کد تایید ممکن نشد.');
    } finally {
      setPending(false);
    }
  }

  async function verifyCode(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(code);

    if (digits.length !== 6) {
      setError('کد تایید ۶ رقمی را کامل وارد کنید.');
      return;
    }

    setError(null);
    setPending(true);
    try {
      await postJson('/api/admin-auth/otp', { action: 'verify', code: digits });
      router.replace(safeNextPath(searchParams.get('next'), '/'));
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تایید کد ممکن نشد.');
      setPending(false);
    }
  }

  return (
    <Card className="w-full max-w-md overflow-hidden shadow-soft">
      <span aria-hidden="true" className="block h-1.5 w-full bg-secondary-container" />

      <div className="flex flex-col gap-lg p-xl">
        <div className="flex flex-col items-center gap-xs text-center">
          <h1 className="font-headline text-section-title text-primary">بوژان</h1>
          <p className="text-body-md text-on-surface-variant">
            {step === 'phone'
              ? 'ورود با رمز یک‌بار مصرف'
              : `کد ۶ رقمی ارسال‌شده به ${toPersianDigits(normalizeDigitsInput(phone))} را وارد کنید.`}
          </p>
        </div>

        {step === 'phone' ? (
          <form onSubmit={requestCode} noValidate className="flex flex-col gap-lg">
            <Input
              name="phone"
              label="شماره موبایل"
              placeholder="۰۹۱۲۳۴۵۶۷۸۹"
              icon="call"
              inputMode="numeric"
              autoComplete="tel"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              {...(error ? { error } : null)}
            />

            <Button type="submit" size="lg" fullWidth loading={pending} trailingIcon="sms">
              دریافت کد تایید
            </Button>
          </form>
        ) : (
          <form onSubmit={verifyCode} noValidate className="flex flex-col gap-lg">
            <Input
              name="code"
              label="کد تایید"
              placeholder="- - - - - -"
              icon="pin"
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              className="tabular text-center tracking-[0.5em]"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              {...(error ? { error } : null)}
            />

            <Button type="submit" size="lg" fullWidth loading={pending} trailingIcon="login">
              ورود به پنل مدیریت
            </Button>

            <button
              type="button"
              onClick={() => {
                setStep('phone');
                setCode('');
                setError(null);
              }}
              className="text-center text-label-md font-medium text-on-surface-variant transition-colors hover:text-primary"
            >
              ویرایش شماره موبایل
            </button>
          </form>
        )}

        <Link
          href="/login"
          className="flex items-center justify-center gap-xs text-label-md font-medium text-on-surface-variant transition-colors hover:text-primary"
        >
          <Icon name="lock" size={18} />
          ورود با رمز عبور
        </Link>

        <p className="flex items-start gap-xs border-t border-paper-border pt-md text-caption leading-relaxed text-outline">
          <Icon name="security" size={16} className="mt-px shrink-0" />
          این صفحه فقط برای کاربران مجاز است. تمام تلاش‌های ورود ثبت می‌شود.
        </p>
      </div>
    </Card>
  );
}
