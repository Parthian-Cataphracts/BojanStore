'use client';

import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import Image from 'next/image';
import { Button, Card, Icon, Input } from '@bojan/ui';
import { formPayload, postJson } from '@/lib/api/submit';
import { SignOutButton } from './SignOutButton';
import type { User } from '@/lib/api/types';

type Errors = Partial<Record<'firstName' | 'lastName' | 'email' | 'avatar' | 'form', string>>;

/** What the picker accepts, matching the formats the API will sniff for. */
const ACCEPTED_IMAGES = 'image/jpeg,image/png,image/webp,image/gif';

/** Screen 16 — Personal information. */
export function ProfileForm({ user }: { user: User }) {
  const [errors, setErrors] = useState<Errors>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  // The uploaded URL is held here and posted with the rest of the form, so a
  // picture the customer picked and then abandoned is never saved.
  const [avatar, setAvatar] = useState<string | null>(user.avatar ?? null);
  const [uploading, setUploading] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  async function pickAvatar(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Reset immediately so choosing the same file twice still fires a change.
    event.target.value = '';
    if (!file) return;

    setErrors((current) => ({ ...current, avatar: undefined }));
    setUploading(true);
    try {
      const body = new FormData();
      body.append('file', file);

      const response = await fetch('/api/upload/avatars', { method: 'POST', body });
      const result = (await response.json().catch(() => null)) as
        | { url?: string; error?: string }
        | null;

      if (!response.ok || !result?.url) {
        throw new Error(result?.error ?? 'بارگذاری تصویر انجام نشد.');
      }

      setAvatar(result.url);
      setSaved(false);
    } catch (cause) {
      setErrors((current) => ({
        ...current,
        avatar: cause instanceof Error ? cause.message : 'بارگذاری تصویر انجام نشد.',
      }));
    } finally {
      setUploading(false);
    }
  }

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
      await postJson('/api/account/profile', {
        ...formPayload(form),
        // Only sent when it changed. An unchanged profile should not re-post
        // the same URL, and the field is omitted entirely rather than sent
        // empty, which is how the API is told to clear the picture.
        ...(avatar !== (user.avatar ?? null) ? { avatar: avatar ?? '' } : null),
      });
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
      <div className="flex flex-col items-center gap-sm">
        <div className="relative">
          {avatar ? (
            <Image
              src={avatar}
              alt=""
              width={96}
              height={96}
              // The picture is decorative here — the name it belongs to is
              // right below in the form, so announcing it twice adds nothing.
              unoptimized={avatar.startsWith('data:')}
              className="h-24 w-24 rounded-full object-cover"
            />
          ) : (
            <span className="flex h-24 w-24 items-center justify-center rounded-full bg-soft-mint font-headline text-headline-lg text-primary">
              {user.firstName.charAt(0)}
            </span>
          )}

          <input
            ref={fileInput}
            type="file"
            accept={ACCEPTED_IMAGES}
            onChange={pickAvatar}
            className="sr-only"
            tabIndex={-1}
            aria-hidden="true"
          />

          <button
            type="button"
            onClick={() => fileInput.current?.click()}
            disabled={uploading}
            aria-label="تغییر تصویر پروفایل"
            className="absolute bottom-0 end-0 flex h-9 w-9 items-center justify-center rounded-full border-2 border-background bg-primary text-on-primary transition-colors hover:bg-primary-container disabled:cursor-not-allowed disabled:opacity-50"
          >
            <Icon name={uploading ? 'progress_activity' : 'edit'} size={18} />
          </button>
        </div>

        {avatar ? (
          <button
            type="button"
            onClick={() => setAvatar(null)}
            className="text-caption text-on-surface-variant underline underline-offset-4 transition-colors hover:text-error"
          >
            حذف تصویر
          </button>
        ) : null}

        {errors.avatar ? (
          <p role="alert" className="text-caption text-error">
            {errors.avatar}
          </p>
        ) : null}
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
          placeholder="example@domain.com"
          defaultValue={user.email ?? ''}
          {...(errors.email ? { error: errors.email } : null)}
        />

        {/*
          A real date input, and the value it round-trips is the ISO one the
          API stores. It used to be a text box pre-filled with `formatDate`'s
          Jalali rendering ("۱۳۷۳/۰۲/۲۸") and a hint asking for Jalali — which
          the API parses with DateOnly.TryParse. Persian digits fail that parse
          outright, so the *whole profile save* was rejected for anyone who had
          a birth date stored, whether they touched the field or not; and an
          ASCII Jalali date like "1373/02/28" parses as the year 1373 AD, which
          is worse than failing. The browser's picker localises its own display,
          so the shopper still reads a Persian date.
        */}
        <Input
          name="birthDate"
          type="date"
          label="تاریخ تولد"
          icon="calendar_today"
          defaultValue={user.birthDate ?? ''}
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
