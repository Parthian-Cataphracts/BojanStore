'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Checkbox, Icon, Input, Select, Textarea, normalizeDigitsInput } from '@bojan/ui';
import type { Address } from '@/lib/api/types';
import { routes } from '@/lib/routes';
import { formPayload, postJson } from '@/lib/api/submit';

/** Province → cities, matching the two-select pattern on screen 15. */
const provinces: Record<string, string[]> = {
  تهران: ['تهران', 'شهریار', 'اسلامشهر', 'ری'],
  البرز: ['کرج', 'فردیس', 'نظرآباد'],
  اصفهان: ['اصفهان', 'کاشان', 'نجف‌آباد'],
  فارس: ['شیراز', 'مرودشت', 'کازرون'],
  'خراسان رضوی': ['مشهد', 'نیشابور', 'سبزوار'],
};

type Errors = Partial<Record<'recipient' | 'phone' | 'province' | 'city' | 'line' | 'postalCode' | 'form', string>>;

/** Screen 15 — Add / edit address. */
export function AddressForm({ address }: { address?: Address }) {
  const router = useRouter();
  const isEdit = Boolean(address);

  const [province, setProvince] = useState(address?.province ?? '');
  const [errors, setErrors] = useState<Errors>({});
  const [saving, setSaving] = useState(false);

  const cities = provinces[province] ?? [];

  function validate(form: HTMLFormElement): Errors {
    const data = new FormData(form);
    const next: Errors = {};

    const recipient = String(data.get('recipient') ?? '').trim();
    const phone = normalizeDigitsInput(String(data.get('phone') ?? ''));
    const postalCode = normalizeDigitsInput(String(data.get('postalCode') ?? ''));
    const line = String(data.get('line') ?? '').trim();

    if (recipient.length < 3) next.recipient = 'نام گیرنده را کامل وارد کنید.';
    if (!/^09\d{9}$/.test(phone)) next.phone = 'شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.';
    if (!data.get('province')) next.province = 'استان را انتخاب کنید.';
    if (!data.get('city')) next.city = 'شهر را انتخاب کنید.';
    if (line.length < 10) next.line = 'آدرس را کامل‌تر بنویسید.';
    if (postalCode.length !== 10) next.postalCode = 'کد پستی باید ۱۰ رقم باشد.';

    return next;
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const found = validate(event.currentTarget);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    setSaving(true);
    try {
      await postJson('/api/account/address', {
        ...formPayload(form),
        ...(address ? { id: address.id } : null),
      });
      router.push(routes.addresses);
      router.refresh();
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ذخیره آدرس انجام نشد.' });
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      <Card className="flex items-start gap-sm p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <div className="flex flex-col gap-xs">
          <h2 className="text-label-md font-label-md text-primary">آدرس را دقیق وارد کنید</h2>
          <p className="text-caption leading-relaxed text-on-surface-variant">
            برای ارسال سریع و بدون مشکل سفارش‌ها، لطفاً اطلاعات آدرس را کامل و دقیق ثبت کنید.
          </p>
        </div>
      </Card>

      <Card className="flex flex-col gap-lg p-lg">
        <Input
          name="recipient"
          label="نام و نام خانوادگی گیرنده"
          placeholder="مثال: علی رضایی"
          defaultValue={address?.recipient ?? ''}
          required
          {...(errors.recipient ? { error: errors.recipient } : null)}
        />

        <Input
          name="phone"
          type="tel"
          inputMode="numeric"
          label="شماره موبایل"
          placeholder="۰۹۱۲۳۴۵۶۷۸۹"
          icon="smartphone"
          defaultValue={address?.phone ?? ''}
          required
          {...(errors.phone ? { error: errors.phone } : null)}
        />

        <div className="grid gap-lg md:grid-cols-2">
          <Select
            name="province"
            label="استان"
            value={province}
            onChange={(event) => setProvince(event.target.value)}
            required
            {...(errors.province ? { error: errors.province } : null)}
          >
            <option value="">انتخاب کنید</option>
            {Object.keys(provinces).map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
          </Select>

          <Select
            name="city"
            label="شهر"
            defaultValue={address?.city ?? ''}
            disabled={cities.length === 0}
            required
            {...(errors.city ? { error: errors.city } : null)}
          >
            <option value="">{cities.length === 0 ? 'ابتدا استان را انتخاب کنید' : 'انتخاب کنید'}</option>
            {cities.map((city) => (
              <option key={city} value={city}>
                {city}
              </option>
            ))}
          </Select>
        </div>

        <Textarea
          name="line"
          label="آدرس کامل"
          placeholder="نام خیابان، کوچه، محله..."
          defaultValue={address?.line ?? ''}
          required
          {...(errors.line ? { error: errors.line } : null)}
        />

        <div className="grid grid-cols-2 gap-lg md:grid-cols-3">
          <Input name="plaque" label="پلاک" inputMode="numeric" />
          <Input name="unit" label="واحد" inputMode="numeric" />
          <Input
            name="postalCode"
            label="کد پستی"
            inputMode="numeric"
            maxLength={10}
            defaultValue={address?.postalCode ?? ''}
            wrapperClassName="col-span-2 md:col-span-1"
            required
            {...(errors.postalCode ? { error: errors.postalCode } : null)}
          />
        </div>

        <Input
          name="notes"
          label="توضیحات تکمیلی (اختیاری)"
          placeholder="نکاتی برای پیدا کردن راحت‌تر آدرس"
        />

        <Checkbox
          name="isDefault"
          defaultChecked={address?.isDefault ?? false}
          label="این آدرس به عنوان آدرس پیش‌فرض ذخیره شود"
        />
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

      <div className="flex flex-col gap-md md:flex-row-reverse md:justify-start">
        <Button type="submit" size="lg" loading={saving} className="md:w-auto md:px-xl">
          {isEdit ? 'ذخیره تغییرات' : 'ذخیره آدرس'}
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="lg"
          onClick={() => router.push(routes.addresses)}
          className="md:w-auto md:px-xl"
        >
          انصراف
        </Button>
      </div>
    </form>
  );
}
