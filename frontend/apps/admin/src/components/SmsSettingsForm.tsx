'use client';

import { useState, type FormEvent } from 'react';
import { Button, Icon, Input, Select } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { ProviderTestResult, SmsSettingsDto } from '@/lib/api/types';

/**
 * The SMS account the shop sends through.
 *
 * Two settings that look similar and are not interchangeable, so the screen
 * says which is which. The <b>line number</b> is the shop's advertising line and
 * carries campaigns. The <b>template</b> belongs to the service line and is the
 * only thing that reaches a number which has blocked advertising SMS — which is
 * most numbers — so it is what every sign-in code goes out through. A shop with
 * a line number and no template can send offers and cannot sign anybody in.
 *
 * The API key never comes back from the server. An empty box means "leave what
 * is stored alone", not "clear it".
 */
export function SmsSettingsForm({ settings }: { settings: SmsSettingsDto }) {
  const [provider, setProvider] = useState(settings.provider);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testPhone, setTestPhone] = useState('');
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const apiKey = String(data.get('apiKey') ?? '').trim();

    setSaving(true);
    setSaved(false);
    setError(null);
    setTestResult(null);

    try {
      await postJson('/api/admin/sms-settings', {
        provider,
        // Omitted entirely when the box was left empty — that is how the API
        // tells "keep it" from "clear it".
        ...(apiKey ? { apiKey } : null),
        lineNumber: String(data.get('lineNumber') ?? '').trim(),
        otpTemplateId: Number(data.get('otpTemplateId')) || 0,
        otpParameterName: String(data.get('otpParameterName') ?? '').trim(),
      });

      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تنظیمات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  async function test() {
    if (!testPhone.trim()) {
      setTestResult({ ok: false, message: 'برای آزمایش، یک شماره موبایل وارد کنید.' });
      return;
    }

    setTesting(true);
    setTestResult(null);

    try {
      const result = await postJson<ProviderTestResult>('/api/admin/sms-test', {
        phone: testPhone.trim(),
      });

      setTestResult({
        ok: result.ok,
        message: result.ok
          ? (result.detail ?? 'پیامک آزمایشی ارسال شد.')
          : (result.error ?? 'ارسال انجام نشد.'),
      });
    } catch (cause) {
      setTestResult({
        ok: false,
        message: cause instanceof Error ? cause.message : 'ارسال انجام نشد.',
      });
    } finally {
      setTesting(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-lg">
      <FormSection
        title="سرویس پیامک"
        icon="sms"
        description="تا وقتی سرویسی انتخاب نشده، کد ورود مشتری‌ها ارسال نمی‌شود و هیچ‌کس نمی‌تواند وارد حساب کاربری شود."
      >
        <Select
          name="provider"
          label="سرویس فعال"
          value={provider}
          onChange={(event) => setProvider(event.target.value)}
        >
          <option value="none">هیچ‌کدام</option>
          <option value="smsir">
            {'sms.ir'}
          </option>
        </Select>

        {provider === 'none' && (
          <p role="alert" className="text-caption leading-relaxed text-error">
            با این گزینه، ورود مشتری‌ها با کد پیامکی کار نمی‌کند.
          </p>
        )}
      </FormSection>

      {provider === 'smsir' && (
        <>
          <FormSection title="حساب" icon="key">
            <Input
              name="apiKey"
              label={
                settings.hasApiKey ? 'کلید وب‌سرویس (ثبت شده — برای تغییر پر کنید)' : 'کلید وب‌سرویس'
              }
              type="password"
              autoComplete="new-password"
              placeholder={settings.hasApiKey ? '••••••••••••••••' : ''}
              className="latin"
            />
            <p className="text-caption leading-relaxed text-on-surface-variant">
              از بخش «پنل برنامه‌نویسان» در <span className="latin">sms.ir</span>. رمزنگاری‌شده
              ذخیره می‌شود و هرگز به مرورگر بازگردانده نمی‌شود.
            </p>
          </FormSection>

          <FormSection
            title="کد ورود (خط خدماتی)"
            icon="lock_open"
            description="کد ورود از خط خدماتی و از روی قالب تأییدشده ارسال می‌شود — تنها راهی که به شماره‌های مسدودکننده‌ی پیامک تبلیغاتی هم می‌رسد."
          >
            <Input
              name="otpTemplateId"
              label="شناسه قالب کد تأیید"
              type="number"
              inputMode="numeric"
              defaultValue={settings.otpTemplateId > 0 ? String(settings.otpTemplateId) : ''}
              placeholder="123456"
              className="latin"
            />
            <p className="text-caption leading-relaxed text-on-surface-variant">
              قالب را در بخش «ارسال سریع» پنل <span className="latin">sms.ir</span> بسازید؛ متن آن
              باید جای کد را با یک پارامتر مشخص کند، مثلاً «کد ورود شما:{' '}
              <span className="latin">#Code#</span>».
            </p>

            <Input
              name="otpParameterName"
              label="نام پارامتر کد در قالب"
              defaultValue={settings.otpParameterName}
              placeholder="Code"
              className="latin"
            />
            <p className="text-caption leading-relaxed text-on-surface-variant">
              دقیقاً همان نامی که داخل قالب استفاده کرده‌اید، بدون علامت{' '}
              <span className="latin">#</span>. اگر این نام با قالب یکی نباشد، پیامک بدون کد ارسال
              می‌شود.
            </p>
          </FormSection>

          <FormSection
            title="پیامک‌های گروهی (خط تبلیغاتی)"
            icon="campaign"
            description="کمپین‌ها و اطلاع‌رسانی‌های عمومی از خط اختصاصی خودتان ارسال می‌شوند."
          >
            <Input
              name="lineNumber"
              label="شماره خط ارسال"
              inputMode="numeric"
              defaultValue={settings.lineNumber}
              placeholder="30004505000017"
              className="latin"
            />
            <p className="text-caption leading-relaxed text-on-surface-variant">
              اختیاری. بدون آن، کد ورود همچنان ارسال می‌شود ولی کمپین‌های پیامکی ارسال نمی‌شوند.
            </p>
          </FormSection>
        </>
      )}

      <div className="flex flex-wrap items-end gap-md">
        <Button type="submit" loading={saving}>
          ذخیره تنظیمات
        </Button>

        <Input
          label="شماره برای پیامک آزمایشی"
          value={testPhone}
          onChange={(event) => setTestPhone(event.target.value)}
          placeholder="09121234567"
          inputMode="tel"
          className="latin"
          wrapperClassName="w-full sm:w-56"
        />

        <Button type="button" variant="outline" loading={testing} onClick={test}>
          ارسال پیامک آزمایشی
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-md">
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
        پیامک آزمایشی از همان قالب کد تأیید و با تنظیمات <strong>ذخیره‌شده</strong> ارسال می‌شود —
        پس اگر چیزی را تغییر داده‌اید، اول ذخیره کنید و بعد آزمایش. این ارسال یک پیامک واقعی است و
        از اعتبار حساب شما کم می‌کند.
      </p>
    </form>
  );
}
