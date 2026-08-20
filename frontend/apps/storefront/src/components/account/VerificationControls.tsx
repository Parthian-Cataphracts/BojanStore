'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { Badge, Button, Icon, Input, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';

/**
 * A local stand-in for `@/lib/api/submit`'s `postJson`.
 *
 * That helper throws a plain `Error` on failure, discarding everything but the
 * message — right for every other form in this app, wrong here: a 429's
 * `retryAfterSeconds` is what seeds the countdown at the real remaining time
 * instead of the 120-second guess, the same reasoning `LoginForm`'s OTP step
 * rests on.
 */
class VerifyError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly retryAfterSeconds?: number,
  ) {
    super(message);
    this.name = 'VerifyError';
  }
}

async function postVerification<T = unknown>(path: string, body?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      credentials: 'same-origin',
      ...(body !== undefined ? { body: JSON.stringify(body) } : null),
    });
  } catch {
    throw new VerifyError('ارتباط با سرور برقرار نشد. اتصال خود را بررسی کنید.', 0);
  }

  const payload = (await response.json().catch(() => null)) as
    | (Record<string, unknown> & { error?: unknown; retryAfterSeconds?: unknown })
    | null;

  if (!response.ok) {
    const message = typeof payload?.error === 'string' ? payload.error : 'انجام این کار ممکن نشد.';
    const retryAfterSeconds =
      typeof payload?.retryAfterSeconds === 'number' ? payload.retryAfterSeconds : undefined;
    throw new VerifyError(message, response.status, retryAfterSeconds);
  }

  return (payload ?? {}) as T;
}

/**
 * Screen 16 — email and phone verification, next to the profile fields.
 *
 * Both share the same countdown shape as `LoginForm`'s OTP resend: a
 * `setTimeout` re-armed by its own effect, seeded either from the backend's
 * default or, on a 429, from `retryAfterSeconds` — so a reload mid-cooldown
 * still shows the real remaining time instead of a guess.
 */
function useCountdown(): [number, (seconds: number) => void] {
  const [seconds, setSeconds] = useState(0);

  useEffect(() => {
    if (seconds <= 0) return;
    const timer = setTimeout(() => setSeconds((current) => Math.max(0, current - 1)), 1000);
    return () => clearTimeout(timer);
  }, [seconds]);

  return [seconds, setSeconds];
}


export function EmailVerificationControl({ verified, hasEmail }: { verified: boolean; hasEmail: boolean }) {
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [cooldown, setCooldown] = useCountdown();

  if (verified) {
    return (
      <Badge tone="success" className="self-start">
        <Icon name="check_circle" size={16} />
        تایید شده
      </Badge>
    );
  }

  if (!hasEmail) return null;

  async function request() {
    setPending(true);
    setError(null);
    try {
      await postVerification('/api/account/email/verify/request');
      setSent(true);
      setCooldown(120);
    } catch (cause) {
      if (cause instanceof VerifyError) {
        if (cause.status === 429) setCooldown(cause.retryAfterSeconds ?? 120);
        setError(cause.message);
      } else {
        setError('ارسال لینک تایید ممکن نشد.');
      }
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="flex flex-col items-start gap-xs">
      <Button
        type="button"
        variant="outline"
        size="sm"
        icon="mail"
        loading={pending}
        disabled={cooldown > 0}
        onClick={request}
      >
        {cooldown > 0 ? `ارسال دوباره تا ${toPersianDigits(cooldown)} ثانیه دیگر` : 'ارسال لینک تایید'}
      </Button>

      {sent && !error && (
        <p className="flex items-center gap-xs text-caption text-primary">
          <Icon name="check_circle" size={14} />
          لینک تایید برای شما ایمیل شد.
        </p>
      )}

      {error && (
        <p role="alert" className="text-caption text-error">
          {error}
        </p>
      )}
    </div>
  );
}

type PhoneMode = 'idle' | 'verify-code' | 'edit' | 'change-code';

export function PhoneVerificationControl({
  verified,
  onPhoneChanged,
}: {
  verified: boolean;
  onPhoneChanged: (phone: string) => void;
}) {
  const [mode, setMode] = useState<PhoneMode>('idle');
  const [newPhone, setNewPhone] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [cooldown, setCooldown] = useCountdown();

  async function requestCurrentPhoneCode() {
    setPending(true);
    setError(null);
    try {
      await postVerification('/api/account/phone/verify/request');
      setMode('verify-code');
      setCooldown(120);
    } catch (cause) {
      if (cause instanceof VerifyError) {
        if (cause.status === 429) setCooldown(cause.retryAfterSeconds ?? 120);
        setError(cause.message);
      } else {
        setError('ارسال کد تایید ممکن نشد.');
      }
    } finally {
      setPending(false);
    }
  }

  async function requestPhoneChange(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(newPhone);

    if (!/^09\d{9}$/.test(digits)) {
      setError('شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.');
      return;
    }

    setPending(true);
    setError(null);
    try {
      await postVerification('/api/account/phone/change/request', { newPhone: digits });
      setNewPhone(digits);
      setMode('change-code');
      setCooldown(120);
    } catch (cause) {
      if (cause instanceof VerifyError) {
        if (cause.status === 429) setCooldown(cause.retryAfterSeconds ?? 120);
        setError(cause.message);
      } else {
        setError('ارسال کد تایید ممکن نشد.');
      }
    } finally {
      setPending(false);
    }
  }

  async function confirmCode(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(code);

    if (!/^\d{4,6}$/.test(digits)) {
      setError('کد تایید را کامل وارد کنید.');
      return;
    }

    setPending(true);
    setError(null);
    try {
      await postVerification('/api/account/phone/verify/confirm', { code: digits });
      setConfirmed(true);
      setCode('');
      const wasChangingPhone = mode === 'change-code';
      setMode('idle');
      if (wasChangingPhone) onPhoneChanged(newPhone);
    } catch (cause) {
      setError(cause instanceof VerifyError ? cause.message : 'تایید کد ممکن نشد.');
    } finally {
      setPending(false);
    }
  }

  if (mode === 'idle') {
    return (
      <div className="flex flex-col items-start gap-xs">
        <div className="flex flex-wrap items-center gap-sm">
          {verified ? (
            <Badge tone="success">
              <Icon name="check_circle" size={16} />
              تایید شده
            </Badge>
          ) : (
            <Button
              type="button"
              variant="outline"
              size="sm"
              icon="sms"
              loading={pending}
              disabled={cooldown > 0}
              onClick={requestCurrentPhoneCode}
            >
              {cooldown > 0
                ? `ارسال دوباره تا ${toPersianDigits(cooldown)} ثانیه دیگر`
                : 'ارسال کد تایید'}
            </Button>
          )}

          <button
            type="button"
            onClick={() => {
              setError(null);
              setNewPhone('');
              setMode('edit');
            }}
            className="flex items-center gap-2xs text-caption text-on-surface-variant underline underline-offset-4 transition-colors hover:text-primary"
          >
            <Icon name="edit" size={14} />
            ویرایش شماره
          </button>
        </div>

        {confirmed && (
          <p className="flex items-center gap-xs text-caption text-primary">
            <Icon name="check_circle" size={14} />
            شماره موبایل تایید شد.
          </p>
        )}

        {error && (
          <p role="alert" className="text-caption text-error">
            {error}
          </p>
        )}
      </div>
    );
  }

  if (mode === 'edit') {
    return (
      <form onSubmit={requestPhoneChange} className="flex flex-col items-start gap-sm">
        <Input
          label="شماره موبایل جدید"
          inputMode="numeric"
          dir="ltr"
          className="ltr-field"
          placeholder="۰۹۱۲۳۴۵۶۷۸۹"
          value={newPhone}
          onChange={(event) => setNewPhone(event.target.value)}
          {...(error ? { error } : null)}
        />

        <div className="flex items-center gap-sm">
          <Button type="submit" size="sm" loading={pending}>
            دریافت کد
          </Button>
          <button
            type="button"
            onClick={() => {
              setMode('idle');
              setError(null);
            }}
            className="text-caption text-on-surface-variant underline underline-offset-4 transition-colors hover:text-primary"
          >
            انصراف
          </button>
        </div>
      </form>
    );
  }

  // 'verify-code' or 'change-code'
  return (
    <form onSubmit={confirmCode} className="flex flex-col items-start gap-sm">
      <Input
        label="کد تایید"
        inputMode="numeric"
        maxLength={6}
        dir="ltr"
        className="ltr-field tabular text-center tracking-[0.5em]"
        value={code}
        onChange={(event) => setCode(event.target.value)}
        {...(error ? { error } : null)}
      />

      <div className="flex items-center gap-sm">
        <Button type="submit" size="sm" loading={pending}>
          تایید کد
        </Button>
        <button
          type="button"
          onClick={() => {
            setMode('idle');
            setCode('');
            setError(null);
          }}
          className="text-caption text-on-surface-variant underline underline-offset-4 transition-colors hover:text-primary"
        >
          انصراف
        </button>
      </div>
    </form>
  );
}
