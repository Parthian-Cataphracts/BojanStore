'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Checkbox, Icon, Input, Textarea, cn, normalizeDigitsInput } from '@bojan/ui';
import { organizationTypes } from '@/lib/mock/business';
import { routes } from '@/lib/routes';
import { formPayload, postJson } from '@/lib/api/submit';

type Errors = Partial<Record<'organization' | 'economicCode' | 'phone' | 'address' | 'postalCode' | 'form', string>>;

/** Screen 63 — Organisation profile, used to issue official invoices. */
export function OrganizationForm() {
  const router = useRouter();
  const [type, setType] = useState<string>(organizationTypes[0]);
  const [errors, setErrors] = useState<Errors>({});
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const organization = String(data.get('organization') ?? '').trim();
    const economicCode = normalizeDigitsInput(String(data.get('economicCode') ?? ''));
    const phone = normalizeDigitsInput(String(data.get('phone') ?? ''));
    const address = String(data.get('address') ?? '').trim();
    const postalCode = normalizeDigitsInput(String(data.get('postalCode') ?? ''));

    if (organization.length < 3) next.organization = 'نام کامل سازمان را وارد کنید.';
    if (economicCode.length !== 11) next.economicCode = 'شناسه ملی باید ۱۱ رقم باشد.';
    if (!/^0\d{10}$/.test(phone)) next.phone = 'شماره تماس باید ۱۱ رقم باشد.';
    if (address.length < 10) next.address = 'آدرس را کامل‌تر بنویسید.';
    if (postalCode.length !== 10) next.postalCode = 'کد پستی باید ۱۰ رقم باشد.';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    try {
      await postJson('/api/account/business-organization', formPayload(form));
      router.push(routes.businessRequests);
      router.refresh();
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ذخیره اطلاعات انجام نشد.' });
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-lg p-lg">
        <h2 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="business_center" size={22} />
          اطلاعات پایه
        </h2>

        <Input
          name="organization"
          label="نام کامل سازمان / شرکت"
          placeholder="مثال: شرکت معماری پایدار ایرانیان"
          required
          {...(errors.organization ? { error: errors.organization } : null)}
        />

        <fieldset className="flex flex-col gap-sm">
          <legend className="mb-sm text-label-md font-medium text-on-surface-variant">
            نوع سازمان
          </legend>
          <div className="flex flex-wrap gap-sm">
            {organizationTypes.map((option) => (
              <button
                key={option}
                type="button"
                aria-pressed={type === option}
                onClick={() => setType(option)}
                className={cn(
                  'rounded-full border px-md py-sm text-label-md font-medium transition-colors',
                  type === option
                    ? 'border-primary bg-soft-mint/40 text-primary'
                    : 'border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
                )}
              >
                {option}
              </button>
            ))}
          </div>
        </fieldset>

        <div className="grid gap-lg md:grid-cols-2">
          <Input
            name="economicCode"
            label="شناسه ملی / کد اقتصادی"
            inputMode="numeric"
            maxLength={11}
            required
            {...(errors.economicCode ? { error: errors.economicCode } : null)}
          />
          <Input
            name="registrationNumber"
            label="شماره ثبت"
            inputMode="numeric"
          />
        </div>
      </Card>

      <Card className="flex flex-col gap-lg p-lg">
        <h2 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="contact_phone" size={22} />
          اطلاعات تماس و آدرس
        </h2>

        <div className="grid gap-lg md:grid-cols-2">
          <Input name="contact" label="نام فرد رابط" placeholder="مثال: علی رضایی" />
          <Input
            name="phone"
            type="tel"
            inputMode="numeric"
            label="تلفن سازمان"
            placeholder="۰۲۱۸۸۸۸۸۸۸۸"
            required
            {...(errors.phone ? { error: errors.phone } : null)}
          />
        </div>

        <Textarea
          name="address"
          label="آدرس سازمان"
          placeholder="استان، شهر، خیابان، پلاک..."
          required
          {...(errors.address ? { error: errors.address } : null)}
        />

        <Input
          name="postalCode"
          label="کد پستی"
          inputMode="numeric"
          maxLength={10}
          required
          wrapperClassName="md:max-w-xs"
          {...(errors.postalCode ? { error: errors.postalCode } : null)}
        />
      </Card>

      <Card className="flex flex-col gap-md p-lg">
        <h2 className="flex items-center gap-sm font-headline text-card-title text-primary">
          <Icon name="receipt_long" size={22} />
          صدور فاکتور
        </h2>
        <Checkbox
          name="officialInvoice"
          defaultChecked
          label="برای این سازمان فاکتور رسمی صادر شود."
          description="فاکتور رسمی شامل مالیات بر ارزش افزوده است."
        />
        <Checkbox name="credit" label="درخواست تسویه اعتباری دارم." />
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
        ثبت اطلاعات سازمان
      </Button>
    </form>
  );
}
