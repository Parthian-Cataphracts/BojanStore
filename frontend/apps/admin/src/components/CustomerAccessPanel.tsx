'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Card, FormStatus, Icon, Input } from '@bojan/ui';
import { postJson } from '@/lib/submit';

/**
 * The two things an operator reaches for when a customer cannot get into their
 * account, or should not be able to.
 *
 * Neither existed. `IsBlocked` had been on the customer record for as long as
 * the panel had a status column and nothing anywhere wrote it — the list could
 * filter by a state the shop had no way to put anybody into. And a customer who
 * could receive neither the sign-in code nor the reset mail had no route back
 * in at all.
 *
 * Owner only, on both. Closing somebody out of their own account and setting
 * the credential to it are not things to hand to whoever is answering the
 * support queue, and the API refuses them for anyone else regardless of what
 * this component decides to draw.
 */
export function CustomerAccessPanel({
  customerId,
  blocked,
}: {
  customerId: string;
  blocked: boolean;
}) {
  const router = useRouter();

  const [working, setWorking] = useState(false);
  const [saved, setSaved] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);

  async function setBlocked(next: boolean) {
    setWorking(true);
    setSaved(null);
    setError(null);
    try {
      // The state asked for rather than a toggle: two operators on this screen
      // pressing the same button should agree about the result.
      await postJson('/api/admin/customer-block', { customerId, blocked: next });
      setSaved(next ? 'دسترسی این مشتری بسته شد.' : 'دسترسی این مشتری باز شد.');
      setConfirming(false);
      // The badge at the top of the page is rendered on the server, so the
      // page is refreshed rather than the state patched here — one answer
      // about whether the account is open, not two that can disagree.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'تغییر وضعیت انجام نشد.');
    } finally {
      setWorking(false);
    }
  }

  async function submitPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const password = String(new FormData(form).get('password') ?? '');

    setWorking(true);
    setSaved(null);
    setError(null);
    try {
      await postJson('/api/admin/customer-password', { customerId, password });
      setSaved('گذرواژه ثبت شد. همه‌ی نشست‌های باز این مشتری بسته شد.');
      form.reset();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت گذرواژه انجام نشد.');
    } finally {
      setWorking(false);
    }
  }

  return (
    <Card className="flex flex-col gap-lg p-lg">
      <div className="flex items-start gap-sm">
        <Icon name="shield_person" size={22} className="mt-2xs shrink-0 text-primary" />
        <div className="flex min-w-0 flex-col gap-2xs">
          <h2 className="font-headline text-card-title text-on-surface">دسترسی حساب</h2>
          <p className="text-caption leading-relaxed text-on-surface-variant">
            بستن دسترسی، سفارش‌ها و تاریخچه‌ی مشتری را نگه می‌دارد و فقط ورود را می‌بندد — برخلاف حذف،
            برگشت‌پذیر است.
          </p>
        </div>
      </div>

      <div className="flex flex-col gap-sm rounded-xl border border-outline-variant p-md">
        <div className="flex flex-wrap items-center justify-between gap-md">
          <span className="flex items-center gap-xs text-body-md text-on-surface">
            <Icon
              name={blocked ? 'lock' : 'lock_open'}
              size={18}
              className={blocked ? 'text-error' : 'text-primary'}
            />
            {blocked ? 'دسترسی بسته است' : 'دسترسی باز است'}
          </span>

          {blocked ? (
            <Button
              type="button"
              variant="outline"
              size="sm"
              icon="lock_open"
              loading={working}
              onClick={() => setBlocked(false)}
            >
              باز کردن دسترسی
            </Button>
          ) : confirming ? (
            <div className="flex items-center gap-sm">
              <Button
                type="button"
                variant="danger"
                size="sm"
                loading={working}
                onClick={() => setBlocked(true)}
              >
                بله، ببند
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setConfirming(false)}
              >
                انصراف
              </Button>
            </div>
          ) : (
            // Asked twice, because this signs the customer out of whatever
            // device they are holding and they will not have been warned.
            <Button
              type="button"
              variant="outline"
              size="sm"
              icon="lock"
              onClick={() => setConfirming(true)}
            >
              بستن دسترسی
            </Button>
          )}
        </div>

        {confirming && !blocked && (
          <p role="status" className="text-caption leading-relaxed text-on-surface-variant">
            با بستن دسترسی، این مشتری از همه‌ی دستگاه‌هایی که همین حالا وارد است خارج می‌شود و نه با
            کد یک‌بارمصرف می‌تواند وارد شود، نه با گذرواژه، و نه با لینک بازیابی.
          </p>
        )}
      </div>

      <form onSubmit={submitPassword} className="flex flex-col gap-sm">
        <Input
          name="password"
          type="password"
          label="تعیین گذرواژه"
          autoComplete="new-password"
          required
          className="latin"
          dir="ltr"
          hint="برای مشتری‌ای که نه پیامک به دستش می‌رسد و نه ایمیل بازیابی. گذرواژه را خودتان به او بدهید — هیچ‌جا دوباره نمایش داده نمی‌شود."
        />

        <div className="flex flex-wrap items-center gap-md">
          <Button type="submit" icon="key" loading={working}>
            ثبت گذرواژه
          </Button>
          <span className="text-caption text-on-surface-variant">
            نشست‌های باز این مشتری بسته می‌شود.
          </span>
        </div>
      </form>

      <FormStatus ok={saved} error={error} />
    </Card>
  );
}
