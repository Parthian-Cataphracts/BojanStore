'use client';

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

      // The session cookie only exists once the second factor has cleared, so
      // `next` is carried to that step rather than followed now — sending the
      // operator to a protected page here would only bounce them back to
      // sign-in, which is what this used to do.
      if (result.requiresTwoFactor) {
        const next = searchParams.get('next');
        router.replace(
          next ? `/login/two-factor?next=${encodeURIComponent(next)}` : '/login/two-factor',
        );
        return;
      }

      router.replace(safeNextPath(searchParams.get('next'), '/'));
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

          {/*
            The one-time-code link used to lead to a sign-in path of its own,
            which asked for a phone number and then opened the panel for
            anybody who knew the fixed code — no password, no account lookup.
            A one-time code is the *second* factor here, not an alternative to
            the first, and the step that asks for it is reached from a
            successful password rather than from a link beside it.
          */}
        </form>

        <p className="flex items-start gap-xs border-t border-paper-border pt-md text-caption leading-relaxed text-outline">
          <Icon name="security" size={16} className="mt-px shrink-0" />
          این صفحه فقط برای کاربران مجاز است. تمام تلاش‌های ورود ثبت می‌شود.
        </p>
      </div>
    </Card>
  );
}
