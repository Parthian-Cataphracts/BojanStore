'use client';

import { useState, type FormEvent } from 'react';
import { Button, Icon, Input } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { MailboxSettingsDto } from '@/lib/api/types';

/** Toggle styled like the one the other settings screens use. */
function Switch({
  name,
  label,
  defaultChecked,
}: {
  name: string;
  label: string;
  defaultChecked: boolean;
}) {
  const [on, setOn] = useState(defaultChecked);

  return (
    <div className="flex items-center justify-between gap-md">
      <span className="text-body-md text-on-surface">{label}</span>
      <input type="hidden" name={name} value={on ? 'true' : 'false'} />
      <button
        type="button"
        role="switch"
        aria-checked={on}
        aria-label={label}
        onClick={() => setOn((value) => !value)}
        className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
          on ? 'bg-primary' : 'bg-outline-variant'
        }`}
      >
        <span
          className={`absolute top-1 h-4 w-4 rounded-full bg-surface-container-lowest transition-all ${
            on ? 'start-6' : 'start-1'
          }`}
        />
      </button>
    </div>
  );
}

/**
 * The support mailbox connection.
 *
 * The password is the one field that never comes back from the server. An empty
 * box means "leave what is stored alone", not "clear it" — which is why the
 * form says whether one is saved rather than showing dots that would suggest a
 * value it does not have.
 */
export function MailboxSettingsForm({ settings }: { settings: MailboxSettingsDto }) {
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const password = String(data.get('password') ?? '');

    setSaving(true);
    setSaved(false);
    setError(null);
    setTestResult(null);

    try {
      await postJson('/api/admin/mailbox-settings', {
        enabled: data.get('enabled') === 'true',
        imapHost: String(data.get('imapHost') ?? '').trim(),
        imapPort: Number(data.get('imapPort')) || 993,
        imapUseSsl: data.get('imapUseSsl') === 'true',
        smtpHost: String(data.get('smtpHost') ?? '').trim(),
        smtpPort: Number(data.get('smtpPort')) || 587,
        smtpUseSsl: data.get('smtpUseSsl') === 'true',
        username: String(data.get('username') ?? '').trim(),
        // Omitted entirely when the box was left empty, which is how the API
        // tells "keep it" from "clear it".
        ...(password ? { password } : null),
        address: String(data.get('address') ?? '').trim(),
        displayName: String(data.get('displayName') ?? '').trim(),
      });

      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تنظیمات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  async function test() {
    setTesting(true);
    setTestResult(null);
    try {
      await postJson('/api/admin/mailbox-test', { confirm: true });
      setTestResult({ ok: true, message: 'اتصال دریافت و ارسال هر دو برقرار شد.' });
    } catch (cause) {
      setTestResult({
        ok: false,
        message: cause instanceof Error ? cause.message : 'اتصال برقرار نشد.',
      });
    } finally {
      setTesting(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-lg">
      <FormSection title="وضعیت" icon="power_settings_new">
        <Switch name="enabled" label="صندوق پستی فعال باشد" defaultChecked={settings.enabled} />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          تا وقتی این گزینه خاموش است، صفحه‌ی صندوق پستی به سرور ایمیل وصل نمی‌شود.
        </p>
      </FormSection>

      <FormSection title="دریافت (IMAP)" icon="inbox">
        <Input name="imapHost" label="میزبان IMAP" defaultValue={settings.imapHost} className="latin" />
        <Input
          name="imapPort"
          label="پورت IMAP"
          type="number"
          defaultValue={String(settings.imapPort)}
          className="latin"
        />
        <Switch name="imapUseSsl" label="اتصال امن (SSL)" defaultChecked={settings.imapUseSsl} />
      </FormSection>

      <FormSection title="ارسال (SMTP)" icon="send">
        <Input name="smtpHost" label="میزبان SMTP" defaultValue={settings.smtpHost} className="latin" />
        <Input
          name="smtpPort"
          label="پورت SMTP"
          type="number"
          defaultValue={String(settings.smtpPort)}
          className="latin"
        />
        <Switch name="smtpUseSsl" label="اتصال امن (SSL)" defaultChecked={settings.smtpUseSsl} />
      </FormSection>

      <FormSection title="حساب" icon="account_circle">
        <Input name="username" label="نام کاربری" defaultValue={settings.username} className="latin" />

        <Input
          name="password"
          label={settings.hasPassword ? 'گذرواژه (ذخیره شده — برای تغییر پر کنید)' : 'گذرواژه'}
          type="password"
          autoComplete="new-password"
          placeholder={settings.hasPassword ? '••••••••' : ''}
          className="latin"
        />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          گذرواژه رمزنگاری‌شده ذخیره می‌شود و هرگز به مرورگر بازگردانده نمی‌شود. خالی گذاشتن این
          فیلد یعنی گذرواژه‌ی فعلی دست‌نخورده بماند.
        </p>

        <Input
          name="address"
          label="نشانی ایمیل پشتیبانی"
          type="email"
          defaultValue={settings.address}
          className="latin"
        />
        <Input name="displayName" label="نام نمایشی فرستنده" defaultValue={settings.displayName} />
      </FormSection>

      <div className="flex flex-wrap items-center gap-md">
        <Button type="submit" loading={saving}>
          ذخیره تنظیمات
        </Button>

        <Button type="button" variant="outline" loading={testing} onClick={test}>
          آزمایش اتصال
        </Button>

        {saved && (
          <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
            <Icon name="check_circle" size={16} />
            ذخیره شد.
          </span>
        )}

        {testResult && (
          <span
            aria-live="polite"
            className={`flex items-center gap-xs text-caption ${
              testResult.ok ? 'text-primary' : 'text-error'
            }`}
          >
            <Icon name={testResult.ok ? 'check_circle' : 'error'} size={16} />
            {testResult.message}
          </span>
        )}

        {error && (
          <span role="alert" className="flex items-center gap-xs text-caption text-error">
            <Icon name="error" size={16} />
            {error}
          </span>
        )}
      </div>

      <p className="text-caption leading-relaxed text-on-surface-variant">
        آزمایش اتصال، تنظیمات <strong>ذخیره‌شده</strong> را می‌آزماید — پس اگر چیزی را تغییر
        داده‌اید، اول ذخیره کنید و بعد آزمایش.
      </p>
    </form>
  );
}
