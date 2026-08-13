'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, buttonClasses, cn } from '@bojan/ui';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

/**
 * The minimum the API enforces — `ChangePasswordValidator.MinimumPasswordLength`.
 *
 * Three layers used to disagree about this number: the form asked for ten, the
 * API's validator for twelve, and the service behind it for eight. A form that
 * says a password is long enough and is then answered by a field error about
 * its length is the worst of the three to be on the wrong side of.
 */
const MINIMUM_LENGTH = 12;

/** Rules the new password must satisfy, checked live as the user types. */
const rules = [
  {
    id: 'length',
    label: 'حداقل ۱۲ نویسه',
    test: (value: string) => value.length >= MINIMUM_LENGTH,
  },
  { id: 'case', label: 'حرف بزرگ و کوچک لاتین', test: (v: string) => /[a-z]/.test(v) && /[A-Z]/.test(v) },
  { id: 'digit', label: 'حداقل یک رقم', test: (value: string) => /\d/.test(value) },
  { id: 'symbol', label: 'حداقل یک نویسه ویژه', test: (value: string) => /[^\w\s]/.test(value) },
];

/** Screen 152 — Password change. */
export function PasswordChangeForm() {
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [done, setDone] = useState(false);

  const passed = rules.filter((rule) => rule.test(next));
  const strongEnough = passed.length === rules.length;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentPassword = String(new FormData(event.currentTarget).get('current') ?? '');

    if (!strongEnough) {
      setError('رمز عبور همه شرط‌ها را برآورده نمی‌کند.');
      return;
    }
    if (next !== confirm) {
      setError('تکرار رمز عبور مطابقت ندارد.');
      return;
    }

    setError(null);
    setSaving(true);
    try {
      await postJson('/api/admin/password', { currentPassword, newPassword: next });

      /*
        This session is already dead and the card below used to pretend
        otherwise. The API rotates the account's security stamp on a password
        change — that is the point of it, since the reason to change a password
        is usually that somebody else has the session — and every request the
        panel makes carries the old stamp, so from here on each page an operator
        opened answered with a 401 they had no explanation for. The cookie is
        dropped instead, which turns a panel that has quietly stopped working
        into a sign-in screen.
      */
      await postJson('/api/admin-auth/logout').catch(() => {
        // The cookie outliving the credential is the state to avoid, and it is
        // not one this can create: the stamp behind it is already stale, so a
        // logout that did not go through leaves a session that opens nothing.
      });

      setDone(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تغییر رمز عبور انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  if (done) {
    return (
      <Card className="flex flex-col items-center gap-md p-xl text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="lock_reset" size={30} />
        </span>
        <h3 className="font-headline text-card-title text-primary">رمز عبور تغییر کرد</h3>
        <p className="max-w-md text-body-md leading-loose text-on-surface-variant">
          همه‌ی نشست‌های فعال بسته شدند — این یکی هم. با رمز جدید دوباره وارد شوید.
        </p>
        {/* A plain anchor, not a router push: the session cookie has just been
            dropped, so this navigation has to leave the client router's cached
            idea of who is signed in behind it. */}
        <a href="/login" className={buttonClasses({ size: 'lg', className: 'gap-xs px-xl' })}>
          <Icon name="login" size={20} />
          ورود دوباره
        </a>
      </Card>
    );
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-2xl flex-col gap-lg">
      <FormSection title="رمز عبور" icon="lock">
        <Input name="current" type="password" label="رمز عبور فعلی" required />

        <Input
          name="next"
          type="password"
          label="رمز عبور جدید"
          value={next}
          onChange={(event) => {
            setNext(event.target.value);
            setError(null);
          }}
          required
        />

        <Input
          name="confirm"
          type="password"
          label="تکرار رمز عبور جدید"
          value={confirm}
          onChange={(event) => {
            setConfirm(event.target.value);
            setError(null);
          }}
          required
          {...(error ? { error } : null)}
        />

        <ul className="flex flex-col gap-xs">
          {rules.map((rule) => {
            const ok = rule.test(next);
            return (
              <li key={rule.id} className="flex items-center gap-xs">
                <Icon
                  name={ok ? 'check_circle' : 'radio_button_unchecked'}
                  size={18}
                  className={cn('shrink-0', ok ? 'text-primary' : 'text-outline-variant')}
                />
                <span className={cn('text-caption', ok ? 'text-primary' : 'text-on-surface-variant')}>
                  {rule.label}
                </span>
              </li>
            );
          })}
        </ul>
      </FormSection>

      <Button type="submit" size="lg" loading={saving} className="self-start px-xl">
        تغییر رمز عبور
      </Button>
    </form>
  );
}
