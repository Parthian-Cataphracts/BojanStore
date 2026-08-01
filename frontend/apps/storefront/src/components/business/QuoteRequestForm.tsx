'use client';

import { useRouter } from 'next/navigation';
import { useState, type ChangeEvent, type FormEvent } from 'react';
import { Button, Card, Icon, Input, Textarea, normalizeDigitsInput } from '@bojan/ui';
import { OptionGroup } from '@/components/checkout/OptionGroup';
import { b2bRequestKinds } from '@/lib/mock/business';
import { routes } from '@/lib/routes';
import { formPayload, postJson } from '@/lib/api/submit';

type Errors = Partial<Record<'organization' | 'contact' | 'phone' | 'email' | 'details' | 'form', string>>;

/** Screen 61 — Request a quote. */
export function QuoteRequestForm() {
  const router = useRouter();
  const [file, setFile] = useState<string | null>(null);
  const [errors, setErrors] = useState<Errors>({});
  const [saving, setSaving] = useState(false);

  function pickFile(event: ChangeEvent<HTMLInputElement>) {
    setFile(event.target.files?.[0]?.name ?? null);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const organization = String(data.get('organization') ?? '').trim();
    const contact = String(data.get('contact') ?? '').trim();
    const phone = normalizeDigitsInput(String(data.get('phone') ?? ''));
    const email = String(data.get('email') ?? '').trim();
    const details = String(data.get('details') ?? '').trim();

    if (organization.length < 3) next.organization = 'نام سازمان را کامل وارد کنید.';
    if (contact.length < 3) next.contact = 'نام فرد رابط را وارد کنید.';
    if (!/^0\d{10}$/.test(phone)) next.phone = 'شماره تماس باید ۱۱ رقم باشد.';
    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) next.email = 'ایمیل معتبر نیست.';
    if (details.length < 20) next.details = 'شرح درخواست را کامل‌تر بنویسید.';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    try {
      await postJson('/api/account/business-quote', formPayload(form));
      router.push(routes.businessSubmitted);
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ثبت درخواست انجام نشد.' });
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-card-title text-primary">نوع درخواست</h2>
        <OptionGroup
          name="kind"
          columns={3}
          options={b2bRequestKinds.map((kind) => ({
            id: kind.id,
            title: kind.label,
            description: kind.description,
          }))}
        />
      </section>

      <Card className="flex flex-col gap-lg p-lg">
        <h2 className="font-headline text-card-title text-primary">اطلاعات تماس</h2>

        <div className="grid gap-lg md:grid-cols-2">
          <Input
            name="organization"
            label="نام سازمان / شرکت"
            placeholder="مثال: شرکت معماری پایدار"
            required
            {...(errors.organization ? { error: errors.organization } : null)}
          />
          <Input
            name="contact"
            label="نام فرد رابط"
            placeholder="مثال: علی رضایی"
            required
            {...(errors.contact ? { error: errors.contact } : null)}
          />
          <Input
            name="phone"
            type="tel"
            inputMode="numeric"
            label="شماره تماس"
            placeholder="۰۲۱۸۸۸۸۸۸۸۸"
            icon="call"
            required
            {...(errors.phone ? { error: errors.phone } : null)}
          />
          <Input
            name="email"
            type="email"
            label="ایمیل"
            hint="اختیاری"
            placeholder="info@company.com"
            icon="mail"
            {...(errors.email ? { error: errors.email } : null)}
          />
        </div>

        <Textarea
          name="details"
          label="شرح درخواست"
          placeholder="اقلام مورد نیاز، تعداد تقریبی، زمان تحویل مورد انتظار..."
          rows={5}
          required
          {...(errors.details ? { error: errors.details } : null)}
        />
      </Card>

      <Card className="flex flex-col gap-md p-lg">
        <h2 className="font-headline text-card-title text-primary">فایل درخواست (اختیاری)</h2>

        <label className="flex cursor-pointer flex-col items-center gap-xs rounded-lg border border-dashed border-outline-variant px-lg py-lg text-center transition-colors hover:bg-surface-container-low">
          <Icon name="cloud_upload" size={28} className="text-primary" />
          <span className="text-label-md font-semibold text-primary">
            نامه درخواست یا لیست محصولات را بارگذاری کنید
          </span>
          <span className="text-caption text-outline">PDF، Word یا Excel — حداکثر ۵ مگابایت</span>
          <input
            type="file"
            accept=".pdf,.doc,.docx,.xls,.xlsx"
            className="sr-only"
            onChange={pickFile}
          />
        </label>

        {file && (
          <p className="flex items-center gap-xs text-caption text-primary">
            <Icon name="attach_file" size={16} />
            {file}
          </p>
        )}
      </Card>

      {errors.form && (
        <p
          role="alert"
          className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
        >
          <Icon name="error" size={20} className="mt-px shrink-0" />
          {errors.form}
        </p>
      )}

      <Button type="submit" size="lg" loading={saving} className="self-start px-xl">
        ارسال درخواست
      </Button>
    </form>
  );
}
