'use client';

import { useState, type FormEvent } from 'react';
import { Button, FormStatus, FormSwitch, Input, Select } from '@bojan/ui';
import { FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { PaymentSettingsDto, ProviderTestResult } from '@/lib/api/types';

/**
 * Which gateway takes the money, and which ways of paying the checkout offers.
 *
 * This screen used to write into the generic settings table, where nothing read
 * it: choosing a gateway and typing a merchant id changed the text on the form
 * and nothing else. Both halves are now real — the gateway settings are what
 * every payment is started against, and the three switches are the shop's own
 * payment-method rows that the checkout validates against.
 *
 * The merchant id is the one field that never comes back from the server. An
 * empty box means "leave what is stored alone", not "clear it", which is why
 * the form says whether one is saved rather than showing dots for a value it
 * does not have.
 */
export function PaymentSettingsForm({ settings }: { settings: PaymentSettingsDto }) {
  const [provider, setProvider] = useState(settings.gateway.provider);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const merchantId = String(data.get('merchantId') ?? '').trim();

    setSaving(true);
    setSaved(false);
    setError(null);
    setTestResult(null);

    try {
      await postJson('/api/admin/payment-settings', {
        provider,
        useSandboxEndpoints: data.get('useSandboxEndpoints') === 'true',
        // Omitted entirely when the box was left empty, which is how the API
        // tells "keep it" from "clear it".
        ...(merchantId ? { merchantId } : null),
        callbackUrl: String(data.get('callbackUrl') ?? '').trim(),
        description: String(data.get('description') ?? '').trim(),
        methods: {
          online: data.get('online') === 'true',
          wallet: data.get('wallet') === 'true',
          cashOnDelivery: data.get('cashOnDelivery') === 'true',
        },
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
      // Always 200 with the outcome in the body — a misconfigured terminal is
      // the operator's to fix, and the sentence saying which is the whole point
      // of the button.
      const result = await postJson<ProviderTestResult>('/api/admin/payment-test', {
        confirm: true,
      });

      setTestResult({
        ok: result.ok,
        message: result.ok
          ? (result.detail ?? 'اتصال برقرار است.')
          : (result.error ?? 'اتصال برقرار نشد.'),
      });
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
      <FormSection
        title="درگاه بانکی"
        icon="credit_card"
        description="تا وقتی درگاهی انتخاب نشده، سفارش‌های آنلاین ثبت می‌شوند ولی جایی برای پرداخت ندارند. پرداخت در محل و کیف پول مستقل از این تنظیم کار می‌کنند."
      >
        <Select
          name="provider"
          label="درگاه فعال"
          value={provider}
          onChange={(event) => setProvider(event.target.value)}
        >
          <option value="none">هیچ‌کدام</option>
          <option value="zarinpal">زرین‌پال</option>
          <option value="sandbox">درگاه آزمایشی داخلی (بدون پرداخت واقعی)</option>
        </Select>

        {provider === 'sandbox' && (
          <p role="alert" className="text-caption leading-relaxed text-error">
            درگاه آزمایشی هر پرداختی را بدون تماس با بانک تأیید می‌کند. فقط برای توسعه است و
            نباید روی فروشگاه واقعی بماند. شارژ کیف پول با این گزینه کار نمی‌کند.
          </p>
        )}

        {provider === 'zarinpal' && (
          <>
            <Input
              name="merchantId"
              label={
                settings.gateway.hasMerchantId
                  ? 'شناسه پذیرنده (ثبت شده — برای تغییر پر کنید)'
                  : 'شناسه پذیرنده'
              }
              placeholder={
                settings.gateway.hasMerchantId
                  ? '••••••••-••••-••••-••••-••••••••••••'
                  : 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
              }
              autoComplete="off"
              className="latin"
            />
            <p className="text-caption leading-relaxed text-on-surface-variant">
              کد ۳۶ کاراکتری که زرین‌پال برای درگاه شما صادر کرده است. رمزنگاری‌شده ذخیره
              می‌شود و هرگز به مرورگر بازگردانده نمی‌شود؛ خالی گذاشتن این فیلد یعنی مقدار فعلی
              دست‌نخورده بماند.
            </p>

            <FormSwitch
              name="useSandboxEndpoints"
              label="استفاده از سرویس تست زرین‌پال (sandbox)"
              defaultChecked={settings.gateway.useSandboxEndpoints}
              hint="در این حالت درخواست‌ها به sandbox.zarinpal.com می‌رود و هیچ مبلغی از کارت کسی کسر نمی‌شود. برای فروشگاه واقعی باید خاموش باشد."
            />
          </>
        )}
      </FormSection>

      {provider !== 'none' && (
        <FormSection title="بازگشت از درگاه" icon="u_turn_left">
          <Input
            name="callbackUrl"
            label="آدرس بازگشت"
            defaultValue={settings.gateway.callbackUrl}
            placeholder="https://example.com/checkout/payment/callback"
            className="latin"
            dir="ltr"
          />
          <p className="text-caption leading-relaxed text-on-surface-variant">
            آدرس کامل صفحه‌ی «تأیید پرداخت» فروشگاه. زرین‌پال این آدرس را با دامنه‌ی ثبت‌شده‌ی
            درگاه مقایسه می‌کند، پس اگر با دامنه‌ی فروشگاه یکی نباشد پرداخت‌ها با خطای
            «مغایرت دامنه» رد می‌شوند.
          </p>

          <Input
            name="description"
            label="توضیح روی صفحه‌ی پرداخت"
            defaultValue={settings.gateway.description}
            placeholder="پرداخت سفارش فروشگاه بوژان"
          />
          <p className="text-caption leading-relaxed text-on-surface-variant">
            متنی که خریدار هنگام وارد کردن اطلاعات کارت می‌بیند. شماره‌ی سفارش خودکار به آن
            اضافه می‌شود.
          </p>
        </FormSection>
      )}

      <FormSection
        title="روش‌های پرداخت"
        icon="payments"
        description="این سه گزینه همان روش‌هایی هستند که در صفحه‌ی تسویه‌حساب به مشتری نشان داده می‌شوند."
      >
        <FormSwitch name="online" label="پرداخت اینترنتی" defaultChecked={settings.methods.online} />
        <FormSwitch name="wallet" label="کیف پول بوژان" defaultChecked={settings.methods.wallet} />
        <FormSwitch
          name="cashOnDelivery"
          label="پرداخت در محل"
          defaultChecked={settings.methods.cashOnDelivery}
        />
      </FormSection>

      <div className="flex flex-wrap items-center gap-md">
        <Button type="submit" loading={saving}>
          ذخیره تنظیمات
        </Button>

        <Button type="button" variant="outline" loading={testing} onClick={test}>
          آزمایش اتصال
        </Button>

        {/* Saving and testing are two separate outcomes and both can be on
            screen at once — a form that saved and then failed its connection
            test has two things to say. */}
        <FormStatus
          ok={saved ? 'ذخیره شد.' : testResult?.ok ? (testResult.message ?? null) : null}
          error={error ?? (testResult && !testResult.ok ? testResult.message : null)}
        />
      </div>

      <p className="text-caption leading-relaxed text-on-surface-variant">
        آزمایش اتصال، تنظیمات <strong>ذخیره‌شده</strong> را می‌آزماید — پس اگر چیزی را تغییر
        داده‌اید، اول ذخیره کنید و بعد آزمایش. این آزمایش یک درخواست پرداخت واقعی به زرین‌پال
        می‌فرستد که هرگز به آن هدایت نمی‌شوید و مبلغی از کسی کسر نمی‌کند.
      </p>
    </form>
  );
}
