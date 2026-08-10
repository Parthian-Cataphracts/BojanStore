'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, normalizeDigitsInput } from '@bojan/ui';
import { safeNextPath } from '@bojan/config/safe-next';
import { postJson } from '@/lib/submit';

/**
 * The second factor, reached only from screen 91 after a password was
 * accepted.
 *
 * The design draws the sign-in card but not this step, so it reuses screen
 * 91's vocabulary — same shell, same rule, same field and button treatments —
 * rather than introducing anything new. There is no phone number to collect
 * here: the code comes from the operator's own authenticator app, and which
 * operator this is was settled by the password.
 */
export function AdminTwoFactorForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function verify(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(code);

    if (digits.length !== 6) {
      setError('کد تایید ۶ رقمی را کامل وارد کنید.');
      return;
    }

    setError(null);
    setPending(true);
    try {
      await postJson('/api/admin-auth/two-factor', { code: digits });
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
            کد ۶ رقمی اپلیکیشن احرازهویت خود را وارد کنید.
          </p>
        </div>

        <form onSubmit={verify} noValidate className="flex flex-col gap-lg">
          <Input
            name="code"
            label="کد تایید دو مرحله‌ای"
            placeholder="- - - - - -"
            icon="pin"
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={6}
            className="latin text-center tracking-[0.5em]"
            value={code}
            onChange={(event) => {
              setCode(event.target.value);
              setError(null);
            }}
            {...(error ? { error } : null)}
          />

          <Button type="submit" size="lg" fullWidth loading={pending} trailingIcon="login">
            ورود به پنل مدیریت
          </Button>
        </form>

        <Link
          href="/login"
          className="flex items-center justify-center gap-xs text-label-md font-medium text-on-surface-variant transition-colors hover:text-primary"
        >
          <Icon name="arrow_forward" size={18} />
          بازگشت به صفحه ورود
        </Link>

        <p className="flex items-start gap-xs border-t border-paper-border pt-md text-caption leading-relaxed text-outline">
          <Icon name="security" size={16} className="mt-px shrink-0" />
          در صورت از دست دادن دسترسی به اپلیکیشن احرازهویت، با مدیر سیستم تماس بگیرید.
        </p>
      </div>
    </Card>
  );
}
