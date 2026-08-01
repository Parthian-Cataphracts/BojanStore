'use client';

import { useState, type FormEvent } from 'react';
import { Button, Icon, Input, Select, Textarea, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { FormLayout, FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

export type MovementKind = 'in' | 'out';
export interface StockMovementProduct {
  id: string;
  title: string;
  stock: number;
}

const reasons: Record<MovementKind, string[]> = {
  in: ['خرید از تأمین‌کننده', 'بازگشت از مشتری', 'اصلاح شمارش انبار', 'انتقال بین انبارها'],
  out: ['فروش', 'مرجوعی به تأمین‌کننده', 'ضایعات یا آسیب', 'نمونه و هدیه', 'اصلاح شمارش انبار'],
};

/** Screens 113 and 114 — Record stock in / stock out. */
export function StockMovementForm({
  kind,
  products,
}: {
  kind: MovementKind;
  products: StockMovementProduct[];
}) {
  const [productId, setProductId] = useState(products[0]?.id ?? '');
  const [quantity, setQuantity] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const product = products.find((item) => item.id === productId);
  const amount = Number(normalizeDigitsInput(quantity) || 0);
  const projected = product ? (kind === 'in' ? product.stock + amount : product.stock - amount) : 0;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (amount <= 0) {
      setError('تعداد باید بیشتر از صفر باشد.');
      return;
    }
    // Refusing to go negative is cheaper than reconciling a broken count later.
    if (kind === 'out' && product && amount > product.stock) {
      setError(
        `موجودی فعلی ${toPersianDigits(product.stock)} عدد است؛ نمی‌توان بیشتر از آن خارج کرد.`,
      );
      return;
    }

    setError(null);
    setSaving(true);
    setSaved(false);
    try {
      await postJson('/api/admin/stock-movements', {
        productId,
        kind,
        quantity: amount,
        reason: String(new FormData(event.currentTarget as HTMLFormElement).get('reason') ?? ''),
      });
      setSaved(true);
      setQuantity('');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت حرکت انبار انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate>
      <FormLayout
        aside={
          <FormSection title="اثر بر موجودی" icon="calculate">
            <dl className="flex flex-col gap-sm text-body-md">
              <div className="flex items-center justify-between">
                <dt className="text-on-surface-variant">موجودی فعلی</dt>
                <dd className="tabular text-on-surface">
                  {toPersianDigits(product?.stock ?? 0)}
                </dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-on-surface-variant">{kind === 'in' ? 'افزایش' : 'کاهش'}</dt>
                <dd className="tabular text-secondary">
                  {kind === 'in' ? '+' : '−'}
                  {toPersianDigits(amount)}
                </dd>
              </div>
              <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
                <dt className="font-semibold text-primary">موجودی پس از ثبت</dt>
                <dd className="tabular font-semibold text-primary">
                  {toPersianDigits(Math.max(0, projected))}
                </dd>
              </div>
            </dl>
          </FormSection>
        }
        actions={
          <>
            <Button type="submit" size="lg" loading={saving} className="px-xl">
              {kind === 'in' ? 'ثبت ورود کالا' : 'ثبت خروج کالا'}
            </Button>
            {saved && (
              <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
                <Icon name="check_circle" size={16} />
                ثبت شد.
              </span>
            )}
          </>
        }
      >
        <FormSection title="کالا و تعداد" icon="inventory_2">
          <Select
            label="محصول"
            value={productId}
            onChange={(event) => {
              setProductId(event.target.value);
              setError(null);
            }}
          >
            {products.map((item) => (
              <option key={item.id} value={item.id}>
                {item.title}
              </option>
            ))}
          </Select>

          <Input
            label="تعداد"
            inputMode="numeric"
            value={quantity}
            onChange={(event) => {
              setQuantity(event.target.value);
              setError(null);
            }}
            {...(error ? { error } : null)}
          />
        </FormSection>

        <FormSection title="جزئیات" icon="description">
          <Select name="reason" label="دلیل" defaultValue={reasons[kind][0]}>
            {reasons[kind].map((reason) => (
              <option key={reason} value={reason}>
                {reason}
              </option>
            ))}
          </Select>

          <Input label="شماره سند / فاکتور" className="latin" placeholder="INV-1405-0042" />
          <Textarea label="توضیحات" rows={3} />
        </FormSection>
      </FormLayout>
    </form>
  );
}
