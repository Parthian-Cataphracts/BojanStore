'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Checkbox, Icon, Input, normalizeDigitsInput } from '@bojan/ui';
import { safeNextPath } from '@/lib/safe-next';
import { postJson } from '@/lib/submit';

type Errors = Partial<Record<'identity' | 'password' | 'form', string>>;

/** Screen 91 — Admin sign-in with a password-visibility toggle and an OTP path. */
export function AdminLoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [showPassword, setShowPassword] = useState(false);
  const [errors, setErrors] = useState<Errors>({});
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const identity = String(data.get('identity') ?? '').trim();
    const password = String(data.get('password') ?? '');

    const isPhone = /^09\d{9}$/.test(normalizeDigitsInput(identity));
    const isEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(identity);
    if (!isPhone && !isEmail) next.identity = 'شماره موبایل یا ایمیل معتبر وارد کنید.';
    if (password.length < 8) next.password = 'رمز عبور حداقل ۸ نویسه است.';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setPending(true);
    try {
      const result = await postJson<{ requiresTwoFactor: boolean }>('/api/admin-auth/login', {
        identity,
        password,
      });

      // The session cookie only exists once the second factor has cleared.
      const destination = result.requiresTwoFactor
        ? '/settings/two-factor'
        : safeNextPath(searchParams.get('next'), '/');

      router.replace(destination);
      router.refresh();
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ورود انجام نشد.' });
      setPending(false);
    }
  }

  return (
    <Card className="w-full max-w-md overflow-hidden shadow-soft">
      {/* Decorative top rule from the design. */}
      <span aria-hidden="true" className="block h-1.5 w-full bg-secondary-container" />

      <div className="flex flex-col gap-lg p-xl">
        <div className="flex flex-col items-center gap-xs text-center">
          <h1 className="font-headline text-section-title text-primary">بوژان</h1>
          <p className="text-body-md text-on-surface-variant">ورود به پنل مدیریت</p>
        </div>

        <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
          <Input
            name="identity"
            label="شماره موبایل یا ایمیل"
            placeholder="۰۹۱۲۳۴۵۶۷۸۹"
            icon="person"
            autoComplete="username"
            required
            {...(errors.identity ? { error: errors.identity } : null)}
          />

          <Input
            name="password"
            type={showPassword ? 'text' : 'password'}
            label="رمز عبور"
            placeholder="••••••••"
            icon="lock"
            autoComplete="current-password"
            required
            suffix={
              <button
                type="button"
                aria-label={showPassword ? 'پنهان کردن رمز' : 'نمایش رمز'}
                onClick={() => setShowPassword((value) => !value)}
                className="pointer-events-auto text-outline transition-colors hover:text-primary"
              >
                <Icon name={showPassword ? 'visibility_off' : 'visibility'} size={20} />
              </button>
            }
            {...(errors.password ? { error: errors.password } : null)}
          />

          <div className="flex flex-wrap items-center justify-between gap-sm">
            <Checkbox name="remember" label="مرا به خاطر بسپار" />
          </div>

          {errors.form && (
            <p
              role="alert"
              className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
            >
              <Icon name="error" size={20} className="mt-px shrink-0" />
              {errors.form}
            </p>
          )}

          <Button type="submit" size="lg" fullWidth loading={pending} trailingIcon="login">
            ورود به پنل مدیریت
          </Button>

          <Link
            href="/login/otp"
            className="flex items-center justify-center gap-xs text-label-md font-medium text-on-surface-variant transition-colors hover:text-primary"
          >
            <Icon name="sms" size={18} />
            ورود با رمز یک‌بار مصرف
          </Link>
        </form>

        <p className="flex items-start gap-xs border-t border-paper-border pt-md text-caption leading-relaxed text-outline">
          <Icon name="security" size={16} className="mt-px shrink-0" />
          این صفحه فقط برای کاربران مجاز است. تمام تلاش‌های ورود ثبت می‌شود.
        </p>
      </div>
    </Card>
  );
}
