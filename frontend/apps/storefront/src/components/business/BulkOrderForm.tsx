'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, Textarea, normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { routes } from '@/lib/routes';
import { formPayload, postJson } from '@/lib/api/submit';

interface LineDraft {
  key: number;
  title: string;
  quantity: string;
  sku: string;
  notes: string;
}

let nextKey = 1;
const emptyLine = (): LineDraft => ({ key: nextKey++, title: '', quantity: '', sku: '', notes: '' });

/** Screen 62 — Bulk order form with a repeatable item list. */
export function BulkOrderForm() {
  const router = useRouter();
  const [lines, setLines] = useState<LineDraft[]>([emptyLine()]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  function update(key: number, patch: Partial<LineDraft>) {
    setLines((current) => current.map((line) => (line.key === key ? { ...line, ...patch } : line)));
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;

    const filled = lines.filter((line) => line.title.trim() && Number(normalizeDigitsInput(line.quantity)) > 0);
    if (filled.length === 0) {
      setError('حداقل یک کالا با نام و تعداد معتبر وارد کنید.');
      return;
    }

    const phone = normalizeDigitsInput(String(new FormData(event.currentTarget).get('phone') ?? ''));
    if (!/^0\d{10}$/.test(phone)) {
      setError('شماره تماس باید ۱۱ رقم باشد.');
      return;
    }

    setError(null);
    setSaving(true);
    try {
      const saved = await postJson<{ code?: string }>('/api/account/business-bulk', {
        ...formPayload(form),
        items: lines,
      });
      router.push(saved.code ? `${routes.businessSubmitted}?code=${encodeURIComponent(saved.code)}` : routes.businessSubmitted);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت درخواست انجام نشد.');
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      <section className="flex flex-col gap-md">
        <div className="flex items-baseline justify-between gap-md">
          <h2 className="font-headline text-card-title text-primary">محصولات درخواستی</h2>
          <span className="tabular text-caption text-outline">
            {toPersianDigits(lines.length)} ردیف
          </span>
        </div>

        {lines.map((line, index) => (
          <Card key={line.key} className="flex flex-col gap-md p-lg">
            <div className="flex items-center justify-between gap-md">
              <span className="tabular text-label-md font-semibold text-primary">
                کالای {toPersianDigits(index + 1)}
              </span>

              {lines.length > 1 && (
                <button
                  type="button"
                  aria-label={`حذف کالای ${toPersianDigits(index + 1)}`}
                  onClick={() => setLines((current) => current.filter((item) => item.key !== line.key))}
                  className="text-outline transition-colors hover:text-error"
                >
                  <Icon name="delete" size={20} />
                </button>
              )}
            </div>

            <Input
              label="نام محصول"
              placeholder="مثال: دفتر پلنر مینیمال روزانه"
              value={line.title}
              onChange={(event) => update(line.key, { title: event.target.value })}
            />

            <div className="grid gap-md sm:grid-cols-2">
              <Input
                label="تعداد (عدد)"
                inputMode="numeric"
                value={line.quantity}
                onChange={(event) => update(line.key, { quantity: event.target.value })}
              />
              <Input
                label="کد کالا (اختیاری)"
                placeholder="مثال: BZ-PLN-A5-001"
                className="latin"
                value={line.sku}
                onChange={(event) => update(line.key, { sku: event.target.value })}
              />
            </div>

            <Textarea
              label="توضیحات تکمیلی"
              placeholder="رنگ، ابعاد خاص یا بسته‌بندی ویژه..."
              rows={2}
              value={line.notes}
              onChange={(event) => update(line.key, { notes: event.target.value })}
            />
          </Card>
        ))}

        <Button
          type="button"
          variant="outline"
          icon="add"
          onClick={() => setLines((current) => [...current, emptyLine()])}
          className="self-start px-lg"
        >
          افزودن کالا
        </Button>
      </section>

      <Card className="flex flex-col gap-lg p-lg">
        <h2 className="font-headline text-card-title text-primary">اطلاعات تماس</h2>
        <div className="grid gap-lg md:grid-cols-2">
          <Input name="organization" label="نام سازمان" placeholder="مثال: مجتمع آموزشی اندیشه" />
          <Input name="contact" label="نام فرد رابط" placeholder="مثال: علی رضایی" />
          <Input
            name="phone"
            type="tel"
            inputMode="numeric"
            label="شماره تماس"
            placeholder="۰۲۱۸۸۸۸۸۸۸۸"
            icon="call"
            required
          />
          <Input name="email" type="email" label="ایمیل" hint="اختیاری" icon="mail" />
        </div>
      </Card>

      {error && (
        <p className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container">
          <Icon name="error" size={20} className="mt-px shrink-0" />
          {error}
        </p>
      )}

      {error && (
        <p
          role="alert"
          className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
        >
          <Icon name="error" size={20} className="mt-px shrink-0" />
          {error}
        </p>
      )}

      <Button type="submit" size="lg" loading={saving} className="self-start px-xl">
        ارسال درخواست خرید عمده
      </Button>
    </form>
  );
}
