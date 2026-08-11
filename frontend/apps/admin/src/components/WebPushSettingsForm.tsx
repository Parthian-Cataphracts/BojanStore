'use client';

import { useState } from 'react';
import { Button, Card, Code, FormStatus, Icon, Input, Sheet, Switch } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { WebPushSettingsDto } from '@/lib/api/types';

/**
 * Browser notifications — the shop's own Web Push identity.
 *
 * There is no account to open and no provider to pay. A browser that agrees to
 * notifications names the push service it is reachable at, and the shop's only
 * credential is a key pair it generates here. So the screen has two things on
 * it: the pair, and a switch.
 *
 * Generating a pair is behind a confirmation because replacing one is silent and
 * permanent. Every browser that ever subscribed recorded the old public key, and
 * a message signed by the new one is from a stranger as far as they are
 * concerned — they simply stop receiving, with nothing to tell them why and no
 * way to reach them to say so.
 */
export function WebPushSettingsForm({ settings }: { settings: WebPushSettingsDto }) {
  const [enabled, setEnabled] = useState(settings.enabled);
  const [subject, setSubject] = useState(settings.subject);
  const [publicKey, setPublicKey] = useState(settings.publicKey);
  const [hasPrivateKey, setHasPrivateKey] = useState(settings.hasPrivateKey);

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [confirmingKeys, setConfirmingKeys] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const ready = publicKey.length > 0 && hasPrivateKey;

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);

    try {
      await postJson('/api/admin/push-settings', { enabled, subject: subject.trim() });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تنظیمات انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  async function generate() {
    setGenerating(true);
    setSaved(false);
    setError(null);

    try {
      const next = await postJson<WebPushSettingsDto>('/api/admin/push-keys', { confirm: true });

      setPublicKey(next.publicKey ?? '');
      setHasPrivateKey(Boolean(next.hasPrivateKey));

      // Generating does not switch the channel on. Whoever pressed this was
      // setting the shop up, and turning a customer-facing channel on for them
      // is a decision they did not make.
      setEnabled(Boolean(next.enabled));
      setConfirmingKeys(false);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ساخت کلید انجام نشد.');
    } finally {
      setGenerating(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <FormSection title="کلید سرور" icon="key">
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          اعلان مرورگر به سرویس خارجی و حساب پولی نیاز ندارد. پیام مستقیم به سرویس خودِ مرورگر
          کاربر می‌رود و تنها چیزی که فروشگاه لازم دارد یک جفت کلید است که همین‌جا ساخته می‌شود.
        </p>

        {ready ? (
          <div className="flex flex-col gap-sm">
            <span className="text-caption text-on-surface-variant">کلید عمومی</span>

            {/* Public by design — a browser cannot subscribe without it. Shown
                in full so the owner can recognise it if they ever need to. */}
            <Code className="block overflow-x-auto whitespace-pre-wrap break-all p-md text-caption">
              {publicKey}
            </Code>

            <p className="flex items-start gap-2xs text-caption leading-relaxed text-on-surface-variant">
              <Icon name="lock" size={18} className="mt-px shrink-0" />
              کلید خصوصی روی سرور رمزنگاری شده و هیچ‌وقت به پنل برنمی‌گردد.
            </p>
          </div>
        ) : (
          <p className="flex items-start gap-2xs rounded-lg bg-surface-container-low px-md py-sm text-body-md leading-relaxed text-on-surface-variant">
            <Icon name="info" size={20} className="mt-px shrink-0" />
            هنوز کلیدی ساخته نشده. تا وقتی کلید نباشد اعلان مرورگر قابل فعال‌سازی نیست.
          </p>
        )}

        <Button
          type="button"
          variant={ready ? 'outline' : 'primary'}
          icon="key"
          className="self-start"
          loading={generating && !confirmingKeys}
          onClick={() => (ready ? setConfirmingKeys(true) : generate())}
        >
          {ready ? 'ساخت کلید جدید' : 'ساخت کلید'}
        </Button>
      </FormSection>

      <FormSection title="تنظیمات کانال" icon="notifications">
        <Switch
          label="ارسال اعلان مرورگر"
          checked={enabled}
          onChange={setEnabled}
          disabled={!ready}
          hint={
            ready
              ? 'وقتی روشن باشد، مشتری می‌تواند در صفحه‌ی اعلان‌های خود این کانال را برای مرورگرش فعال کند.'
              : 'ابتدا کلید بسازید.'
          }
        />

        <Input
          label="آدرس تماس"
          value={subject}
          onChange={(event) => {
            setSubject(event.target.value);
            setSaved(false);
          }}
          placeholder="mailto:info@example.com"
          hint="سرویس‌های مرورگر از این آدرس برای تماس با فروشگاه استفاده می‌کنند. فقط mailto: یا https:"
          className="latin"
          dir="ltr"
        />

        <div className="flex flex-wrap items-center gap-md">
          <Button type="button" loading={saving} onClick={save}>
            ذخیره تنظیمات
          </Button>

          <FormStatus ok={saved ? 'ذخیره شد.' : null} error={error} />
        </div>
      </FormSection>

      <Sheet
        open={confirmingKeys}
        onClose={() => setConfirmingKeys(false)}
        title="ساخت کلید جدید"
        footer={
          <div className="flex flex-col gap-sm sm:flex-row-reverse">
            <Button variant="danger" size="lg" loading={generating} onClick={generate} className="sm:px-xl">
              کلید جدید بساز
            </Button>
            <Button variant="ghost" size="lg" onClick={() => setConfirmingKeys(false)} className="sm:px-xl">
              انصراف
            </Button>
          </div>
        }
      >
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          با ساخت کلید جدید، ارتباط با تمام مرورگرهایی که تا امروز اعلان را فعال کرده‌اند قطع
          می‌شود. آن‌ها دیگر اعلانی دریافت نمی‌کنند و راهی برای اطلاع دادن به آن‌ها وجود ندارد —
          هر کدام باید دوباره خودشان اعلان را فعال کنند. این کار برگشت‌پذیر نیست.
        </p>

        <Card className="mt-md flex items-start gap-sm bg-surface-container-low p-md">
          <Icon name="warning" size={20} className="mt-px shrink-0 text-error" />
          <span className="text-caption leading-relaxed text-on-surface-variant">
            فقط وقتی این کار لازم است که کلید خصوصی لو رفته باشد یا سرور کلیدها را از دست داده
            باشد.
          </span>
        </Card>
      </Sheet>
    </div>
  );
}
