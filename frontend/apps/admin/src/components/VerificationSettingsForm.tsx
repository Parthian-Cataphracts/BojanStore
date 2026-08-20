'use client';

import { useState } from 'react';
import { Button, FormStatus, Switch } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { VerificationSettingsDto } from '@/lib/api/types';

/**
 * تایید ایمیل و شماره — whether sign-up requires proving an email or phone
 * before the account can use it.
 *
 * Two switches, one save, the same shape as `WebPushSettingsForm`: nothing
 * here reaches the backend until "ذخیره تنظیمات" is pressed, so an owner
 * exploring the two options never commits one by accident.
 */
export function VerificationSettingsForm({ settings }: { settings: VerificationSettingsDto }) {
  const [requireEmail, setRequireEmail] = useState(settings.requireEmailVerification);
  const [requirePhone, setRequirePhone] = useState(settings.requirePhoneVerification);

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);

    try {
      await postJson(
        '/api/admin/verification-settings',
        { requireEmailVerification: requireEmail, requirePhoneVerification: requirePhone },
        { method: 'PUT' },
      );
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تنظیمات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <FormSection title="الزام تایید" icon="verified_user">
      <Switch
        label="الزام تایید ایمیل"
        checked={requireEmail}
        onChange={(next) => {
          setRequireEmail(next);
          setSaved(false);
        }}
        hint="وقتی روشن باشد، مشتری برای استفاده از ایمیل ثبت‌شده باید آن را تایید کند."
      />

      <Switch
        label="الزام تایید شماره موبایل"
        checked={requirePhone}
        onChange={(next) => {
          setRequirePhone(next);
          setSaved(false);
        }}
        hint="وقتی روشن باشد، مشتری برای استفاده از شماره موبایل ثبت‌شده باید آن را تایید کند."
      />

      <div className="flex flex-wrap items-center gap-md">
        <Button type="button" loading={saving} onClick={save}>
          ذخیره تنظیمات
        </Button>

        <FormStatus ok={saved ? 'ذخیره شد.' : null} error={error} />
      </div>
    </FormSection>
  );
}
