'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Icon, Input, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes, withReturnTo } from '@/lib/routes';
import { AuthCard } from './AuthCard';
import { AuthSwitch } from './AuthSwitch';
import { AuthTerms } from './AuthTerms';

type Errors = Partial<Record<'phone' | 'email' | 'password' | 'form', string>>;

/** Mirrors the API's PasswordPolicy, so the form can say why without a round trip. */
const MIN_PASSWORD = 8;

/**
 * Screen 10 — registering with a phone, an email and a password.
 *
 * Its own screen rather than a mode of the sign-in form, because it asks for
 * different things and can be linked to directly.
 *
 * The email is required and not decorative: it is the only channel a forgotten
 * password can be recovered through, and recovering by SMS would put the whole
 * flow back on the delivery path this exists to route around.
 */
export function RegisterForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const next = searchParams.get('next');

  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();

    const digits = normalizeDigitsInput(phone);
    const address = email.trim();
    const found: Errors = {};

    if (!/^09\d{9}$/.test(digits)) {
      found.phone = 'شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.';
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address)) {
      found.email = 'ایمیل معتبر وارد کنید.';
    }

    // The same three rules the API enforces, said here so the customer is not
    // told about them one round trip at a time.
    if (password.length < MIN_PASSWORD) {
      found.password = `رمز عبور باید حداقل ${toPersianDigits(MIN_PASSWORD)} نویسه باشد.`;
    } else if (!/[a-zA-Z؀-ۿ]/.test(password) || !/\d/.test(password)) {
      found.password = 'رمز عبور باید ترکیبی از حرف و عدد باشد.';
    }

    setErrors(found);
    if (Object.keys(found).length > 0) return;

    setPending(true);
    try {
      await postJson('/api/auth/register', { phone: digits, email: address, password });

      // Straight to the profile step — the account exists but has no name yet,
      // which is exactly the state screen 52 is for. It carries `?next=` on, so
      // registering in the middle of a checkout comes back to it once the
      // profile is done rather than ending on the account page.
      router.replace(withReturnTo(routes.completeProfile, next));
      router.refresh();
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ثبت‌نام انجام نشد.' });
      setPending(false);
    }
  }

  return (
    <AuthCard
      icon="person_add"
      title="ساخت حساب کاربری"
      caption="با رمز عبور وارد شوید، حتی وقتی پیامک به دستتان نمی‌رسد."
      above={<AuthSwitch active="register" next={next} />}
      below={<AuthTerms />}
    >
      <form onSubmit={submit} noValidate className="flex flex-col gap-md md:gap-lg">
        {/*
          Phone, e-mail and password all hold left-to-right values on a
          right-to-left page. `.ltr-field` turns the field around so the digits
          and the caret run the way they are typed; the e-mail also takes
          `.latin`, because an address is Latin text and the Persian face
          renders it noticeably wider and looser.
        */}
        <Input
          label="شماره موبایل"
          inputMode="numeric"
          autoComplete="tel"
          placeholder="۰۹۱۲۳۴۵۶۷۸۹"
          icon="call"
          dir="ltr"
          className="ltr-field"
          required
          hint="شماره‌ای که کد تأیید و پیگیری سفارش به آن ارسال می‌شود."
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
          {...(errors.phone ? { error: errors.phone } : null)}
        />

        <Input
          label="ایمیل"
          type="email"
          autoComplete="email"
          placeholder="name@example.com"
          icon="mail"
          dir="ltr"
          className="latin"
          required
          hint="تنها راه بازیابی رمز عبور در صورت فراموشی."
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          {...(errors.email ? { error: errors.email } : null)}
        />

        <Input
          label="رمز عبور"
          type="password"
          autoComplete="new-password"
          icon="lock"
          dir="ltr"
          className="ltr-field"
          required
          hint={`دست‌کم ${toPersianDigits(MIN_PASSWORD)} نویسه و ترکیبی از حرف و عدد باشد.`}
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          {...(errors.password ? { error: errors.password } : null)}
        />

        <Button type="submit" size="lg" fullWidth loading={pending}>
          ساخت حساب
        </Button>

        {errors.form && (
          <p role="alert" className="flex items-start gap-xs text-caption text-error">
            <Icon name="error" size={16} className="mt-px shrink-0" />
            {errors.form}
          </p>
        )}
      </form>
    </AuthCard>
  );
}
