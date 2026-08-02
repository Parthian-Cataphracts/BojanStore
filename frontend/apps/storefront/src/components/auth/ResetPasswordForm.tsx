'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';

const MIN_PASSWORD = 8;

/**
 * Setting a new password from an emailed link.
 *
 * Finishing here does not sign anyone in — it sends them to the sign-in screen
 * to use what they just chose. A reset that opened a session would turn a
 * forwarded email, or one read on a shared machine, into a takeover; the extra
 * step is the difference between proving you can read the inbox and proving you
 * know the password.
 */
export function ResetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();

    if (password.length < MIN_PASSWORD) {
      setError(`رمز عبور باید حداقل ${toPersianDigits(MIN_PASSWORD)} نویسه باشد.`);
      return;
    }

    if (!/[a-zA-Z؀-ۿ]/.test(password) || !/\d/.test(password)) {
      setError('رمز عبور باید ترکیبی از حرف و عدد باشد.');
      return;
    }

    // Only on this screen. Registering has no confirm box because a wrong
    // password there is recoverable in a second; here the customer is already
    // locked out and a typo would lock them out again.
    if (password !== confirm) {
      setError('دو رمز عبور یکسان نیستند.');
      return;
    }

    setError(null);
    setPending(true);
    try {
      await postJson('/api/auth/password', { action: 'reset', token, password });
      router.replace(`${routes.login}?reset=1&method=password`);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تغییر رمز عبور انجام نشد.');
      setPending(false);
    }
  }

  // A link that arrived without its token cannot be completed, and the form
  // would only fail on submit — better to say so before anything is typed.
  if (!token) {
    return (
      <Card className="flex w-full max-w-md flex-col items-center gap-md p-xl text-center shadow-soft">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-error-container text-error">
          <Icon name="link_off" size={32} />
        </span>
        <h1 className="font-headline text-display-md text-primary">لینک نامعتبر است</h1>
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          این لینک ناقص است یا منقضی شده. دوباره درخواست بازیابی بدهید.
        </p>
        <Link
          href={routes.forgotPassword}
          className="text-label-md font-label-md text-primary underline underline-offset-4"
        >
          درخواست لینک تازه
        </Link>
      </Card>
    );
  }

  return (
    <Card className="w-full max-w-md p-xl shadow-soft">
      <div className="mb-lg flex flex-col items-center gap-sm text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="lock_reset" size={32} />
        </span>
        <h1 className="font-headline text-display-md text-primary">رمز عبور تازه</h1>
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          رمز عبور جدید خود را وارد کنید. پس از ثبت، با آن وارد شوید.
        </p>
      </div>

      <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
        <Input
          label="رمز عبور جدید"
          type="password"
          autoComplete="new-password"
          icon="lock"
          required
          hint={`حداقل ${toPersianDigits(MIN_PASSWORD)} نویسه، ترکیبی از حرف و عدد.`}
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />

        <Input
          label="تکرار رمز عبور"
          type="password"
          autoComplete="new-password"
          icon="lock"
          required
          value={confirm}
          onChange={(event) => setConfirm(event.target.value)}
          {...(error ? { error } : null)}
        />

        <Button type="submit" size="lg" fullWidth loading={pending}>
          ثبت رمز عبور
        </Button>
      </form>
    </Card>
  );
}
