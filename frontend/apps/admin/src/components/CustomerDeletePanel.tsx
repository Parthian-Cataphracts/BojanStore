'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Card, FormStatus, Icon } from '@bojan/ui';
import { postJson } from '@/lib/submit';

/**
 * Removing a customer account for good.
 *
 * The endpoint has existed since customer management was built and nothing in
 * the panel ever called it — the allow-list carried `customers/delete` and no
 * screen had a button, so «حذف کاربر» was a thing the API could do and an
 * operator could not.
 *
 * Two presses, not one. The row it removes is a person's account, the action
 * has no undo, and a delete sitting one click away from «ویرایش» on a list is
 * the kind of thing that gets pressed by accident.
 *
 * A customer who has ever ordered is refused by the API rather than by this
 * component — their orders name them, and an order whose customer is gone is a
 * record nobody can answer questions about. That refusal arrives here as its
 * own sentence, so the operator is told to suspend the account instead of
 * being told the delete "failed".
 */
export function CustomerDeletePanel({
  customerId,
  name,
  hasHistory,
}: {
  customerId: string;
  name: string;
  hasHistory: boolean;
}) {
  const router = useRouter();

  const [confirming, setConfirming] = useState(false);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function remove() {
    setWorking(true);
    setError(null);
    try {
      await postJson('/api/admin/customer-delete', { customerId });
      // Back to the list: the page this sits on describes a row that no longer
      // exists, and refreshing it would render a 404 under the operator.
      router.push('/customers');
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'حذف حساب انجام نشد.');
      setWorking(false);
      setConfirming(false);
    }
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <div className="flex items-center gap-xs">
        <Icon name="delete" size={20} className="text-error" />
        <h2 className="text-card-title text-primary">حذف حساب</h2>
      </div>

      {hasHistory ? (
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          این مشتری سابقه‌ی خرید دارد، پس حسابش حذف نمی‌شود — سفارش‌های ثبت‌شده به نام او هستند و
          سفارشی که مشتری‌اش پاک شده باشد دیگر قابل پیگیری نیست. برای بستن دسترسی، از «مسدود کردن»
          در همین صفحه استفاده کنید.
        </p>
      ) : (
        <>
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            حساب «{name}» به‌طور کامل حذف می‌شود. این کار برگشت‌پذیر نیست.
          </p>

          {confirming ? (
            <div className="flex flex-wrap items-center gap-sm">
              <Button variant="danger" onClick={remove} disabled={working}>
                {working ? 'در حال حذف...' : 'بله، حذف کن'}
              </Button>
              <Button variant="ghost" onClick={() => setConfirming(false)} disabled={working}>
                انصراف
              </Button>
            </div>
          ) : (
            <div>
              <Button variant="danger" onClick={() => setConfirming(true)}>
                حذف این حساب
              </Button>
            </div>
          )}
        </>
      )}

      <FormStatus error={error} />
    </Card>
  );
}
