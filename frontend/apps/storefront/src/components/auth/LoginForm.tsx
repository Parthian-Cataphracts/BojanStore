'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useState, type FormEvent } from 'react';
import { Button, Icon, Input, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';
import { safeNextPath } from '@bojan/config/safe-next';
import { AuthCard } from './AuthCard';
import { AuthSwitch } from './AuthSwitch';
import { AuthTerms } from './AuthTerms';

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

  /**
   * Seconds left before a second code may be requested. The backend refuses a
   * resend for as long as the phone's current challenge is still live — this
   * is only the countdown that mirrors that on screen, not the thing enforcing
   * it, so a stale or reset value here costs nothing but a disabled button
   * that re-enables a little early: the request underneath still gets refused
   * with the real remaining time on that resend attempt.
   */
  const [resendIn, setResendIn] = useState(0);

  // A `setTimeout` re-armed by the effect re-running, not `setInterval`: the
  // countdown then has exactly one real dependency — `resendIn` itself — so
  // there is nothing here for exhaustive-deps to flag and nothing that drifts
  // out of step with a value React already knows changed.
  useEffect(() => {
    if (resendIn <= 0) return;
    const timer = setTimeout(() => setResendIn((seconds) => Math.max(0, seconds - 1)), 1000);
    return () => clearTimeout(timer);
  }, [resendIn]);

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

  /**
   * Shared by the phone step's submit and the OTP step's resend button — both
   * are "ask the backend for a code", differing only in what happens after.
   */
  async function sendOtp(digits: string): Promise<boolean> {
    setError(null);
    setPending(true);
    try {
      const result = await postJson<{ resendAfter?: number }>('/api/auth/otp/request', {
        phone: digits,
      });
      // 120 is the fallback for mock mode's plain `{ ok: true }`, which never
      // added `resendAfter` — a mismatch here only makes the countdown wrong,
      // never the refusal, since mock mode has no backend to refuse against.
      setResendIn(result.resendAfter ?? 120);
      return true;
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال کد تایید ممکن نشد.');
      return false;
    } finally {
      setPending(false);
    }
  }

  async function requestCode(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(phone);

    if (!/^09\d{9}$/.test(digits)) {
      setError('شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.');
      return;
    }

    if (await sendOtp(digits)) {
      setStep('otp');
    }
  }

  /** The OTP step's own resend — same request, but the customer stays put. */
  async function resendCode() {
    const digits = normalizeDigitsInput(phone);
    if (await sendOtp(digits)) {
      setCode('');
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
    <AuthCard
      icon={step === 'otp' ? 'sms' : step === 'password' ? 'lock' : 'person'}
      title={heading}
      caption={caption}
      below={<AuthTerms />}
      above={
        <>
          {/* Hidden on the code step: by then the customer is mid-flow and a
              tab that throws away the code they are waiting for is a trap. */}
          {step !== 'otp' && <AuthSwitch active="login" next={next} />}

          {/* Set by the reset screen, which finishes here rather than opening
              a session of its own. Without it the customer arrives at a plain
              sign-in form with no sign that the password they just chose
              actually took. */}
          {searchParams.get('reset') === '1' && step !== 'otp' && (
            <p
              role="status"
              className="mb-md flex items-start gap-xs rounded-lg bg-soft-mint px-md py-sm text-body-md text-primary md:mb-lg"
            >
              <Icon name="check_circle" size={20} className="mt-px shrink-0" />
              رمز عبور شما تغییر کرد. حالا با آن وارد شوید.
            </p>
          )}
        </>
      }
    >

      {step === 'phone' && (
        <form onSubmit={requestCode} className="flex flex-col gap-md md:gap-lg">
          <Input
            label="شماره موبایل"
            inputMode="numeric"
            autoComplete="tel"
            placeholder="۰۹۱۲۳۴۵۶۷۸۹"
            hint="کد ورود به همین شماره پیامک می‌شود."
            icon="call"
            dir="ltr"
            className="ltr-field"
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            {...(error ? { error } : null)}
          />

          <Button type="submit" size="lg" fullWidth loading={pending}>
            دریافت کد تأیید
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
        <form onSubmit={verifyCode} className="flex flex-col gap-md md:gap-lg">
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

          {/*
            Disabled for the same span the backend actually refuses a resend
            for — see `sendOtp`. A shopper who never asks again never sees
            this at all; one who does gets a real reason a code has not
            arrived is worth pressing again for, not a button that looked
            live and quietly failed.
          */}
          <button
            type="button"
            onClick={resendCode}
            disabled={resendIn > 0 || pending}
            className="text-center text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:text-on-surface-variant"
          >
            {resendIn > 0
              ? `ارسال دوباره تا ${toPersianDigits(resendIn)} ثانیه دیگر`
              : 'ارسال دوباره‌ی کد'}
          </button>

          <button
            type="button"
            onClick={() => {
              setCode('');
              setResendIn(0);
              switchTo('phone');
            }}
            className="text-center text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
          >
            ویرایش شماره موبایل
          </button>
        </form>
      )}

      {step === 'password' && (
        <form onSubmit={signInWithPassword} className="flex flex-col gap-md md:gap-lg">
          <Input
            label="شماره موبایل یا ایمیل"
            autoComplete="username"
            // Both forms of this value read left-to-right, so the field does
            // too — but the Persian-digit half of the placeholder needs the
            // Persian face, which is why this is `.ltr-field` and not `.latin`.
            placeholder="۰۹۱۲۳۴۵۶۷۸۹"
            hint="همان شماره یا ایمیلی که با آن ثبت‌نام کرده‌اید."
            icon="person"
            dir="ltr"
            className="ltr-field"
            value={identity}
            onChange={(event) => setIdentity(event.target.value)}
          />

          <Input
            label="رمز عبور"
            type="password"
            autoComplete="current-password"
            icon="lock"
            dir="ltr"
            className="ltr-field"
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

    </AuthCard>
  );
}
