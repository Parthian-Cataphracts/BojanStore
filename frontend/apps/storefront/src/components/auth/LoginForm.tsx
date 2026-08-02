'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';
import { safeNextPath } from '@/lib/safe-next';
import { AuthSwitch } from './AuthSwitch';

type Step = 'phone' | 'otp' | 'password';

/**
 * Screen 09 — signing in, by code or by password.
 *
 * The code is the default because it is what most customers already use and
 * needs nothing remembered. The password is the second door, and it exists for
 * one reason: SMS to Iranian networks does not always arrive, and a shop whose
 * only way in is a text message loses those customers silently.
 *
 * Neither method is checked here. `/api/auth/otp/verify` and `/api/auth/login`
 * own the attempt limits and set the session cookie, so nothing about a
 * challenge or a credential is held in this component and reloading mid-flow
 * cannot reset anything.
 */
export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const next = searchParams.get('next');

  // `?method=password` opens straight on that step, so the "forgot password"
  // screen and any link that knows the customer has one can send them there
  // without a detour through the code form.
  const [step, setStep] = useState<Step>(
    searchParams.get('method') === 'password' ? 'password' : 'phone',
  );
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [identity, setIdentity] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  /** Where a completed sign-in lands. Registering has its own destination. */
  function done(isNewUser: boolean) {
    router.replace(
      isNewUser ? routes.completeProfile : safeNextPath(next, routes.account),
    );
    // The session cookie is new — re-render server components against it.
    router.refresh();
  }

  function switchTo(nextStep: Step) {
    setStep(nextStep);
    setError(null);
  }

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
      done(result.isNewUser);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تایید کد ممکن نشد.');
      setPending(false);
    }
  }

  async function signInWithPassword(event: FormEvent) {
    event.preventDefault();

    if (identity.trim().length === 0 || password.length === 0) {
      setError('شماره موبایل یا ایمیل و رمز عبور را وارد کنید.');
      return;
    }

    setError(null);
    setPending(true);
    try {
      await postJson('/api/auth/login', { identity: identity.trim(), password });
      done(false);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ورود انجام نشد.');
      setPending(false);
    }
  }

  const heading = {
    phone: 'ورود به بوژان',
    otp: 'تایید شماره موبایل',
    password: 'ورود با رمز عبور',
  }[step];

  const caption = {
    phone: 'شماره موبایل خود را وارد کنید تا کد تایید برایتان ارسال شود.',
    otp: `کد ۵ رقمی ارسال‌شده به ${toPersianDigits(normalizeDigitsInput(phone))} را وارد کنید.`,
    password: 'با شماره موبایل یا ایمیل و رمز عبور خود وارد شوید.',
  }[step];

  return (
    <Card className="w-full max-w-md p-xl shadow-soft">
      {/* Hidden on the code step: by then the customer is mid-flow and a tab
          that throws away the code they are waiting for is a trap. */}
      {step !== 'otp' && <AuthSwitch active="login" next={next} />}

      {/* Set by the reset screen, which finishes here rather than opening a
          session of its own. Without it the customer arrives at a plain sign-in
          form with no sign that the password they just chose actually took. */}
      {searchParams.get('reset') === '1' && step !== 'otp' && (
        <p
          role="status"
          className="mb-lg flex items-start gap-xs rounded-lg bg-soft-mint px-md py-sm text-body-md text-primary"
        >
          <Icon name="check_circle" size={20} className="mt-px shrink-0" />
          رمز عبور شما تغییر کرد. حالا با آن وارد شوید.
        </p>
      )}

      <div className="mb-lg flex flex-col items-center gap-sm text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name={step === 'otp' ? 'sms' : step === 'password' ? 'lock' : 'person'} size={32} />
        </span>
        <h1 className="font-headline text-display-md text-primary">{heading}</h1>
        <p className="text-body-md leading-relaxed text-on-surface-variant">{caption}</p>
      </div>

      {step === 'phone' && (
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

          {/*
            The second door, and the reason it is here: a code that never
            arrives is the common failure, not a rare one.
          */}
          <button
            type="button"
            onClick={() => switchTo('password')}
            className="flex items-center justify-center gap-xs text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
          >
            <Icon name="lock" size={18} />
            کد پیامک دریافت نمی‌کنید؟ با رمز عبور وارد شوید
          </button>
        </form>
      )}

      {step === 'otp' && (
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
            تایید و ادامه
          </Button>

          <button
            type="button"
            onClick={() => {
              setCode('');
              switchTo('phone');
            }}
            className="text-center text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
          >
            ویرایش شماره موبایل
          </button>
        </form>
      )}

      {step === 'password' && (
        <form onSubmit={signInWithPassword} className="flex flex-col gap-lg">
          <Input
            label="شماره موبایل یا ایمیل"
            autoComplete="username"
            placeholder="۰۹۱۲۳۴۵۶۷۸۹ یا example@domain.com"
            icon="person"
            value={identity}
            onChange={(event) => setIdentity(event.target.value)}
          />

          <Input
            label="رمز عبور"
            type="password"
            autoComplete="current-password"
            icon="lock"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            {...(error ? { error } : null)}
          />

          <Button type="submit" size="lg" fullWidth loading={pending}>
            ورود
          </Button>

          <div className="flex flex-col gap-sm text-center">
            <Link
              href={routes.forgotPassword}
              className="text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
            >
              رمز عبور خود را فراموش کرده‌اید؟
            </Link>

            <button
              type="button"
              onClick={() => switchTo('phone')}
              className="flex items-center justify-center gap-xs text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
            >
              <Icon name="sms" size={18} />
              ورود با کد پیامکی
            </button>
          </div>
        </form>
      )}

      <p className="mt-lg text-center text-caption leading-relaxed text-on-surface-variant">
        با ورود یا ثبت‌نام، <Link href={routes.terms} className="text-primary underline">قوانین و مقررات</Link> و{' '}
        <Link href={routes.privacy} className="text-primary underline">حریم خصوصی</Link> بوژان را
        می‌پذیرید.
      </p>
    </Card>
  );
}
