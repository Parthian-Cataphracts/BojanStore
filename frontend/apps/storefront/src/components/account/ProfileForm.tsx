'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, formatDate } from '@bojan/ui';
import { formPayload, postJson } from '@/lib/api/submit';
import { SignOutButton } from './SignOutButton';
import type { User } from '@/lib/api/types';

type Errors = Partial<Record<'firstName' | 'lastName' | 'email' | 'form', string>>;

/** Screen 16 — Personal information. */
export function ProfileForm({ user }: { user: User }) {
  const [errors, setErrors] = useState<Errors>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

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
    setSaved(false);
    try {
      await postJson('/api/account/profile', formPayload(form));
      setSaved(true);
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ذخیره اطلاعات انجام نشد.' });
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      {/* Avatar */}
      <div className="flex justify-center">
        <div className="relative">
          <span className="flex h-24 w-24 items-center justify-center rounded-full bg-soft-mint font-headline text-headline-lg text-primary">
            {user.firstName.charAt(0)}
          </span>
          <button
            type="button"
            aria-label="تغییر تصویر پروفایل"
            className="absolute bottom-0 end-0 flex h-9 w-9 items-center justify-center rounded-full border-2 border-background bg-primary text-on-primary transition-colors hover:bg-primary-container"
          >
            <Icon name="edit" size={18} />
          </button>
        </div>
      </div>

      <Card className="flex flex-col gap-lg p-lg">
        <div className="grid gap-lg md:grid-cols-2">
          <Input
            name="firstName"
            label="نام"
            defaultValue={user.firstName}
            required
            {...(errors.firstName ? { error: errors.firstName } : null)}
          />
          <Input
            name="lastName"
            label="نام خانوادگی"
            defaultValue={user.lastName}
            required
            {...(errors.lastName ? { error: errors.lastName } : null)}
          />
        </div>

        <Input
          type="tel"
          inputMode="numeric"
          label="شماره موبایل"
          icon="phone_iphone"
          defaultValue={user.phone}
          disabled
          hint="برای تغییر شماره موبایل باید دوباره با کد تایید وارد شوید."
        />

        <Input
          name="email"
          type="email"
          label="ایمیل"
          icon="mail"
          placeholder="email@example.com"
          defaultValue={user.email ?? ''}
          {...(errors.email ? { error: errors.email } : null)}
        />

        <Input
          name="birthDate"
          label="تاریخ تولد"
          icon="calendar_today"
          defaultValue={user.birthDate ? formatDate(user.birthDate) : ''}
          placeholder="۱۳۷۳/۰۲/۲۸"
          hint="به تاریخ شمسی وارد کنید."
        />

        <Input name="city" label="شهر" icon="location_on" defaultValue={user.city ?? ''} />
      </Card>

      <p className="flex items-start gap-xs text-caption leading-relaxed text-on-surface-variant">
        <Icon name="lock" size={16} className="mt-px shrink-0" />
        اطلاعات شما فقط برای تجربه خرید بهتر و پیگیری سفارش‌ها استفاده می‌شود.
      </p>

      {saved && (
        <p className="flex items-center gap-xs rounded-lg bg-soft-mint px-md py-sm text-body-md text-primary">
          <Icon name="check_circle" size={20} />
          تغییرات ذخیره شد.
        </p>
      )}

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
        <Button type="submit" size="lg" fullWidth loading={saving}>
          ذخیره تغییرات
        </Button>

        <SignOutButton />
      </div>
    </form>
  );
}
