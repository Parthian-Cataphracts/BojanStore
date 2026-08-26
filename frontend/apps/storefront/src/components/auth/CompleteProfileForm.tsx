'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { safeNextPath } from '@bojan/config/safe-next';
import { Button, Card, Checkbox, Icon, Input, JalaliDateInput } from '@bojan/ui';
import { routes } from '@/lib/routes';
import { formPayload, postJson } from '@/lib/api/submit';

type Errors = Partial<Record<'firstName' | 'lastName' | 'email' | 'form', string>>;

/** Screen 52 — First-run profile completion. */
export function CompleteProfileForm() {
  const router = useRouter();
  const searchParams = useSearchParams();

  /**
   * Where this screen hands the customer on to.
   *
   * The account page for somebody who came here straight from signing up, and
   * the page they were trying to reach when they were asked to sign in — set by
   * the middleware and carried this far by `withReturnTo`. Both buttons below
   * use it: a shopper who skips the form is no less in the middle of a checkout
   * than one who fills it in, and dropping them on the account page was the
   * whole complaint.
   *
   * Re-checked rather than trusted, because by now it has been through the
   * address bar and anybody can type there.
   */
  const destination = safeNextPath(searchParams.get('next'), routes.account);

  const [errors, setErrors] = useState<Errors>({});
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const firstName = String(data.get('firstName') ?? '').trim();
    const lastName = String(data.get('lastName') ?? '').trim();
    const email = String(data.get('email') ?? '').trim();

    if (!firstName) next.firstName = 'نام را وارد کنید.';
    if (!lastName) next.lastName = 'نام خانوادگی را وارد کنید.';
    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) next.email = 'ایمیل معتبر نیست.';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    try {
      await postJson('/api/account/profile', formPayload(form));
      router.push(destination);
      router.refresh();
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ذخیره اطلاعات انجام نشد.' });
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      <Card className="flex items-start gap-md p-lg">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="waving_hand" size={24} />
        </span>
        <div className="flex flex-col gap-xs">
          <h2 className="text-body-lg font-label-md text-primary">به بوژان خوش آمدید!</h2>
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            برای تجربه یک خرید سفارشی و لذت‌بخش، لطفاً اطلاعات خود را تکمیل کنید.
          </p>
        </div>
      </Card>

      <Card className="flex flex-col gap-lg p-lg">
        <div className="grid gap-lg md:grid-cols-2">
          <Input
            name="firstName"
            label="نام"
            placeholder="مثال: علی"
            required
            {...(errors.firstName ? { error: errors.firstName } : null)}
          />
          <Input
            name="lastName"
            label="نام خانوادگی"
            placeholder="مثال: رضایی"
            required
            {...(errors.lastName ? { error: errors.lastName } : null)}
          />
        </div>

        <Input
          name="email"
          type="email"
          label="ایمیل"
          placeholder="name@example.com"
          hint="اختیاری — برای ارسال فاکتور و بازیابی رمز عبور"
          icon="mail"
          dir="ltr"
          className="latin"
          {...(errors.email ? { error: errors.email } : null)}
        />

        <Input name="city" label="شهر" placeholder="مثلاً اصفهان" icon="place" />

        {/* See ProfileForm: a text box asking for Jalali produced a value the
            API cannot parse, or one it parses as the wrong millennium. */}
        <JalaliDateInput
          name="birthDate"
          label="تاریخ تولد"
          hint="اختیاری — برای هدیه تولد و پیشنهادهای مناسب شما"
        />

        <Checkbox
          name="optIn"
          label="مایل هستم پیشنهادهای ویژه و اخبار بوژان را دریافت کنم."
        />
      </Card>

      {errors.form && (
        <p
          role="alert"
          className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
        >
          <Icon name="error" size={20} className="mt-px shrink-0" />
          {errors.form}
        </p>
      )}

      <div className="flex flex-col gap-md">
        <Button type="submit" size="lg" fullWidth loading={saving} trailingIcon="arrow_back">
          ذخیره و ادامه
        </Button>

        <Button
          type="button"
          variant="ghost"
          size="lg"
          fullWidth
          onClick={() => router.push(destination)}
        >
          بعداً تکمیل می‌کنم
        </Button>
      </div>
    </form>
  );
}
