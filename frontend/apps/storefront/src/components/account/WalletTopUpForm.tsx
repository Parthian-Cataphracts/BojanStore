'use client';

import { useState, type FormEvent } from 'react';
import { Button, Icon, Input, JalaliDateInput, formatPrice, normalizeDigitsInput } from '@bojan/ui';
import type { WalletOverview } from '@/lib/api/types';
import { formPayload, postJson } from '@/lib/api/submit';

/** Amounts worth one tap, in Toman. */
const PRESETS = [100_000, 200_000, 500_000, 1_000_000];

interface Props {
  wallet: WalletOverview;
}

/**
 * Screen 58's "افزایش اعتبار".
 *
 * Two routes, and which of them exists is the store's decision, not this
 * component's: the gateway is always offered, and the card-to-card form appears
 * only where `manualTopUpEnabled` says an operator is reading that queue.
 * Neither one credits anything here — the gateway route hands back a URL to pay
 * at, and the card-to-card route files a request that sits pending until
 * somebody confirms the transfer against the bank statement.
 */
export function WalletTopUpForm({ wallet }: Props) {
  const [amount, setAmount] = useState('');
  const [manual, setManual] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const parsed = Number(normalizeDigitsInput(amount).replace(/[^\d]/g, '')) || 0;
  const tooSmall = parsed > 0 && parsed < wallet.minimumTopUp;
  const tooLarge = parsed > wallet.maximumTopUp;

  function validate(): string | null {
    if (parsed <= 0) return 'مبلغ را وارد کنید.';
    if (tooSmall) return `حداقل مبلغ شارژ ${formatPrice(wallet.minimumTopUp)} است.`;
    if (tooLarge) return `حداکثر مبلغ شارژ در هر درخواست ${formatPrice(wallet.maximumTopUp)} است.`;
    return null;
  }

  async function payByGateway(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const invalid = validate();
    if (invalid) {
      setError(invalid);
      return;
    }

    setError(null);
    setPending(true);
    try {
      const started = await postJson<{ paymentUrl: string }>('/api/account/wallet-topup', {
        amount: parsed,
      });
      // Leaving the site is the point: the balance moves when the gateway
      // answers on the way back, not when this returns.
      window.location.assign(started.paymentUrl);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'شارژ کیف پول انجام نشد.');
      setPending(false);
    }
  }

  async function fileTransfer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const invalid = validate();
    if (invalid) {
      setError(invalid);
      return;
    }

    const payload = formPayload(event.currentTarget);
    setError(null);
    setPending(true);
    try {
      await postJson('/api/account/wallet-topup-manual', { ...payload, amount: parsed });
      // A full reload rather than local state: the pending request now belongs
      // in the list the server renders above this form.
      window.location.reload();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت درخواست انجام نشد.');
      setPending(false);
    }
  }

  const amountField = (
    <div className="flex flex-col gap-sm">
      <Input
        name="amount"
        inputMode="numeric"
        label="مبلغ شارژ (تومان)"
        value={amount}
        onChange={(event) => setAmount(event.target.value)}
        hint={`از ${formatPrice(wallet.minimumTopUp)} تا ${formatPrice(wallet.maximumTopUp)}`}
        {...(tooSmall || tooLarge ? { error: ' ' } : null)}
      />

      <div className="flex flex-wrap gap-sm">
        {PRESETS.filter((preset) => preset >= wallet.minimumTopUp && preset <= wallet.maximumTopUp).map(
          (preset) => (
            <button
              key={preset}
              type="button"
              onClick={() => setAmount(String(preset))}
              className="rounded-full border border-outline-variant px-md py-xs text-caption text-on-surface-variant transition-colors hover:border-primary hover:text-primary"
            >
              {formatPrice(preset)}
            </button>
          ),
        )}
      </div>
    </div>
  );

  return (
    <section className="flex flex-col gap-md">
      <h2 className="font-headline text-display-md text-primary">افزایش اعتبار</h2>

      {error && (
        <p
          role="alert"
          className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
        >
          <Icon name="error" size={20} className="mt-px shrink-0" />
          {error}
        </p>
      )}

      {wallet.manualTopUpEnabled && (
        <div className="flex gap-xs rounded-lg bg-surface-container-low p-xs" role="tablist">
          {[
            { key: false, label: 'پرداخت آنلاین', icon: 'credit_card' },
            { key: true, label: 'کارت به کارت', icon: 'sync_alt' },
          ].map((tab) => (
            <button
              key={String(tab.key)}
              type="button"
              role="tab"
              aria-selected={manual === tab.key}
              onClick={() => {
                setManual(tab.key);
                setError(null);
              }}
              className={`flex flex-1 items-center justify-center gap-xs rounded-md px-md py-sm text-label-md transition-colors ${
                manual === tab.key
                  ? 'bg-surface-container-lowest text-primary shadow-ambient'
                  : 'text-on-surface-variant hover:text-primary'
              }`}
            >
              <Icon name={tab.icon} size={18} />
              {tab.label}
            </button>
          ))}
        </div>
      )}

      {manual && wallet.manualTopUpEnabled ? (
        <form onSubmit={fileTransfer} noValidate className="flex flex-col gap-md">
          {amountField}

          <Input
            name="trackingNumber"
            label="شماره پیگیری واریز"
            required
            hint="شماره‌ای که بانک پس از انتقال به شما داد."
          />

          {/* Jalali, like the rest of the shop. This is a shopper copying a
              date off a bank receipt they read in Persian, so the field they
              copy it into has to be the same calendar. `yearsAhead={0}`
              replaces the `max` that stopped a future date. */}
          <JalaliDateInput
            name="paidOn"
            label="تاریخ واریز"
            required
            yearsBack={1}
            yearsAhead={0}
          />

          <Input name="note" label="توضیح (اختیاری)" />

          {wallet.receiptRequired && (
            <p className="flex items-start gap-xs rounded-lg bg-soft-mint/40 px-md py-sm text-caption leading-relaxed text-on-surface-variant">
              <Icon name="info" size={18} className="mt-px shrink-0 text-primary" />
              تصویر رسید را از طریق پشتیبانی ارسال کنید. درخواست شما پس از بررسی و
              تأیید واریز، به اعتبار کیف پول اضافه می‌شود.
            </p>
          )}

          <Button type="submit" size="lg" fullWidth loading={pending} icon="send">
            ثبت درخواست شارژ
          </Button>

          <p className="text-caption leading-relaxed text-outline">
            اعتبار بلافاصله اضافه نمی‌شود. پس از بررسی واریز توسط پشتیبانی، مبلغ به
            کیف پول شما افزوده و به شما اطلاع داده می‌شود.
          </p>
        </form>
      ) : (
        <form onSubmit={payByGateway} noValidate className="flex flex-col gap-md">
          {amountField}

          <Button type="submit" size="lg" fullWidth loading={pending} icon="credit_card">
            پرداخت و شارژ کیف پول
          </Button>

          <p className="text-caption leading-relaxed text-outline">
            پس از پرداخت موفق، مبلغ به‌صورت خودکار به کیف پول شما اضافه می‌شود.
          </p>
        </form>
      )}
    </section>
  );
}
