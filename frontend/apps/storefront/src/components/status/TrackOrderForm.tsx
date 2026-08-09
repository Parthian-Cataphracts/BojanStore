'use client';

import Link from 'next/link';
import { useState, type FormEvent } from 'react';
import { Badge, Button, Card, Icon, Input, buttonClasses, normalizeDigitsInput } from '@bojan/ui';
import { OrderTimeline } from '@/components/account/OrderTimeline';
import type { OrderDetail } from '@/lib/api/types';
import { orderStatusMeta } from '@/lib/mock/orders';
import { routes } from '@/lib/routes';

type Errors = Partial<Record<'number' | 'phone', string>>;

/**
 * Screen 30 — Guest order tracking.
 *
 * Lookup goes through `GET /api/orders/track`, this app's own route handler —
 * a client component can't import `lib/api/cart` directly, since that module
 * pulls in the server-only session reader. A visitor needs both the order
 * number and the phone it was placed with, and neither alone should reveal
 * anything.
 */
export function TrackOrderForm() {
  const [errors, setErrors] = useState<Errors>({});
  const [notFound, setNotFound] = useState(false);
  const [result, setResult] = useState<OrderDetail | null>(null);
  const [checking, setChecking] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const rawNumber = String(data.get('number') ?? '').trim();
    const phone = normalizeDigitsInput(String(data.get('phone') ?? ''));

    if (!rawNumber) next.number = 'شماره سفارش را وارد کنید.';
    if (!/^09\d{9}$/.test(phone)) next.phone = 'شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.';

    setErrors(next);
    setNotFound(false);
    setResult(null);
    if (Object.keys(next).length > 0) return;

    setChecking(true);
    try {
      const response = await fetch(
        `/api/orders/track?${new URLSearchParams({ number: rawNumber, phone })}`,
      );
      if (response.ok) setResult((await response.json()) as OrderDetail);
      else setNotFound(true);
    } finally {
      setChecking(false);
    }
  }

  const status = result ? orderStatusMeta[result.status] : null;

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-lg p-lg">
        <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
          <div className="grid gap-lg md:grid-cols-2">
            <Input
              name="number"
              label="شماره سفارش"
              placeholder="مثال: BZ-1024"
              icon="receipt_long"
              required
              {...(errors.number ? { error: errors.number } : null)}
            />
            <Input
              name="phone"
              type="tel"
              inputMode="numeric"
              label="شماره موبایل"
              placeholder="۰۹۱۲۳۴۵۶۷۸۹"
              icon="call"
              dir="ltr"
              className="ltr-field"
              required
              {...(errors.phone ? { error: errors.phone } : null)}
            />
          </div>

          <Button type="submit" size="lg" loading={checking} className="md:w-auto md:self-start md:px-xl">
            پیگیری سفارش
          </Button>
        </form>

        {notFound && (
          <p className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container">
            <Icon name="error" size={20} className="mt-px shrink-0" />
            سفارشی با این شماره و موبایل پیدا نشد. اطلاعات را بررسی کنید یا با پشتیبانی تماس بگیرید.
          </p>
        )}
      </Card>

      {result && status && (
        <section className="flex flex-col gap-lg">
          <Card className="flex flex-wrap items-center justify-between gap-md p-lg">
            <div className="flex flex-col gap-xs">
              <span className="text-caption text-on-surface-variant">شماره سفارش</span>
              <span className="tabular text-body-lg font-label-md text-primary">
                #{result.number}
              </span>
            </div>
            <Badge tone={status.tone}>{status.label}</Badge>
          </Card>

          <OrderTimeline steps={result.timeline} />

          <Card className="flex flex-col gap-md p-lg">
            {[
              { label: 'نحوه ارسال', value: result.shippingMethod, icon: 'local_shipping' },
              { label: 'آدرس گیرنده', value: result.shippingAddress, icon: 'place' },
              ...(result.trackingCode
                ? [{ label: 'کد رهگیری مرسوله', value: result.trackingCode, icon: 'travel_explore' }]
                : []),
            ].map((row) => (
              <div
                key={row.label}
                className="flex flex-col gap-xs border-b border-paper-border pb-md last:border-0 last:pb-0"
              >
                <span className="flex items-center gap-xs text-caption text-on-surface-variant">
                  <Icon name={row.icon} size={16} />
                  {row.label}
                </span>
                <span className="text-body-md leading-relaxed text-on-surface">{row.value}</span>
              </div>
            ))}
          </Card>

          <Link
            href={`${routes.orders}/${result.id}`}
            className={buttonClasses({ variant: 'outline', className: 'self-start px-xl' })}
          >
            مشاهده جزئیات کامل سفارش
          </Link>
        </section>
      )}
    </div>
  );
}
