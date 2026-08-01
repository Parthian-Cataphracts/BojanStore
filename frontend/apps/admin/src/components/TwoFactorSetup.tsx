'use client';

import { useState, type FormEvent } from 'react';
import { Badge, Button, Card, Code, Icon, Input, normalizeDigitsInput } from '@bojan/ui';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

const BASE32_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

/**
 * A fresh TOTP secret, generated in this browser and never sent anywhere
 * until the operator proves they can produce a code from it. `ConfirmTwoFactor`
 * only persists a secret once `Totp.Verify` accepts a code against it, so a
 * shared or predictable secret here would make every operator's second
 * factor the same one — crypto.getRandomValues, not a literal string, is
 * what makes this an actual secret.
 */
function randomTotpSecret(length = 16): string {
  const bytes = crypto.getRandomValues(new Uint8Array(length));
  return Array.from(bytes, (byte) => BASE32_ALPHABET[byte % BASE32_ALPHABET.length]).join('');
}

/** Screen 153 — Two-factor setup. */
export function TwoFactorSetup() {
  const [enabled, setEnabled] = useState(false);
  const [secret] = useState(randomTotpSecret);
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [verifying, setVerifying] = useState(false);

  async function verify(event: FormEvent) {
    event.preventDefault();
    const digits = normalizeDigitsInput(code);

    if (digits.length !== 6) {
      setError('کد تایید ۶ رقمی را کامل وارد کنید.');
      return;
    }

    setError(null);
    setVerifying(true);
    try {
      await postJson('/api/admin/two-factor', { code: digits, secret });
      setEnabled(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تایید کد انجام نشد.');
    } finally {
      setVerifying(false);
    }
  }

  if (enabled) {
    return (
      <div className="flex max-w-2xl flex-col gap-lg">
        <Card className="flex items-start gap-md p-lg">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-soft-mint text-primary">
            <Icon name="verified_user" size={24} />
          </span>
          <div className="flex flex-col gap-xs">
            <span className="flex items-center gap-sm">
              <h3 className="text-body-lg font-semibold text-primary">تایید دو مرحله‌ای فعال است</h3>
              <Badge tone="mint">فعال</Badge>
            </span>
            <p className="text-body-md leading-relaxed text-on-surface-variant">
              از این پس هنگام ورود، کد یک‌بارمصرف از اپلیکیشن احرازهویت شما خواسته می‌شود.
            </p>
          </div>
        </Card>

        {/*
          No recovery-code path exists on the backend — ConfirmTwoFactorAsync
          only ever sets a secret, never a set of bypass codes. Showing
          invented ones here would tell an operator they have a recovery
          option that silently does not work the day they need it.
        */}
        <Card className="flex items-start gap-sm p-md">
          <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
          <p className="text-caption leading-relaxed text-on-surface-variant">
            در صورت از دست دادن دسترسی به اپلیکیشن احرازهویت، برای غیرفعال‌سازی تایید دو مرحله‌ای با
            مدیر سیستم تماس بگیرید.
          </p>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex max-w-2xl flex-col gap-lg">
      <FormSection
        title="۱. اسکن کد در اپلیکیشن"
        icon="qr_code_2"
        description="کد زیر را در Google Authenticator، Authy یا هر اپ سازگار با TOTP وارد کنید."
      >
        <div className="flex flex-col items-center gap-md rounded-lg border border-dashed border-outline-variant p-xl">
          <Icon name="qr_code_2" size={96} className="text-primary" />
          <p className="text-caption text-on-surface-variant">
            نمی‌توانید اسکن کنید؟ کد زیر را دستی وارد کنید:
          </p>
          <Code className="rounded border border-outline-variant px-md py-sm text-body-md font-semibold text-primary">
            {secret}
          </Code>
        </div>
      </FormSection>

      <form onSubmit={verify} noValidate>
        <FormSection title="۲. تایید کد" icon="pin">
          <Input
            label="کد ۶ رقمی اپلیکیشن"
            inputMode="numeric"
            maxLength={6}
            placeholder="- - - - - -"
            className="latin text-center tracking-[0.5em]"
            value={code}
            onChange={(event) => {
              setCode(event.target.value);
              setError(null);
            }}
            {...(error ? { error } : null)}
          />

          <Button type="submit" size="lg" loading={verifying} className="self-start px-xl">
            فعال‌سازی
          </Button>
        </FormSection>
      </form>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          توصیه می‌شود همه کاربران با دسترسی مالک یا مدیر محصول، تایید دو مرحله‌ای را فعال کنند.
        </p>
      </Card>
    </div>
  );
}
