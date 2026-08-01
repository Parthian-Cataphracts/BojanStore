'use client';

import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { useState, type ChangeEvent, type FormEvent } from 'react';
import { Button, Card, Icon, Select, Textarea, cn, toPersianDigits } from '@bojan/ui';
import type { OrderDetail } from '@/lib/api/types';
import { returnReasons } from '@/lib/mock/returns';
import { routes } from '@/lib/routes';
import { formPayload, postJson } from '@/lib/api/submit';

const MAX_IMAGES = 3;

type Errors = Partial<Record<'item' | 'reason' | 'note' | 'form', string>>;

/** Screen 35 — Return request. */
export function ReturnRequestForm({ order }: { order: OrderDetail }) {
  const router = useRouter();
  const [selected, setSelected] = useState<string | null>(order.items[0]?.productId ?? null);
  const [images, setImages] = useState<{ name: string; url: string }[]>([]);
  const [errors, setErrors] = useState<Errors>({});
  const [saving, setSaving] = useState(false);

  function addImages(event: ChangeEvent<HTMLInputElement>) {
    const files = [...(event.target.files ?? [])].slice(0, MAX_IMAGES - images.length);
    setImages((current) => [
      ...current,
      ...files.map((file) => ({ name: file.name, url: URL.createObjectURL(file) })),
    ]);
    // Allow re-picking the same file after a removal.
    event.target.value = '';
  }

  function removeImage(name: string) {
    setImages((current) => {
      const target = current.find((image) => image.name === name);
      if (target) URL.revokeObjectURL(target.url);
      return current.filter((image) => image.name !== name);
    });
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    if (!selected) next.item = 'کالای مرجوعی را انتخاب کنید.';
    if (!data.get('reason')) next.reason = 'دلیل مرجوعی را انتخاب کنید.';

    const note = String(data.get('note') ?? '').trim();
    if (data.get('reason') === 'سایر دلایل' && note.length < 10) {
      next.note = 'برای «سایر دلایل» توضیح بنویسید.';
    }

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    try {
      // Images need multipart; they follow once the upload endpoint exists.
      const created = await postJson<{ id?: string }>('/api/account/return-request', {
        ...formPayload(form),
        orderId: order.id,
        items: selected,
      });
      router.push(routes.returnStatus(created.id ?? 'r-1'));
      router.refresh();
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ثبت درخواست انجام نشد.' });
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg pb-[104px] md:pb-0">
      <Card className="flex items-start gap-sm p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-body-md leading-relaxed text-on-surface-variant">
          اگر کالا با سفارش شما مغایرت دارد یا مشکلی در محصول وجود دارد، می‌توانید درخواست مرجوعی
          ثبت کنید.
        </p>
      </Card>

      {/* Item picker */}
      <fieldset className="flex flex-col gap-sm">
        <legend className="mb-sm text-label-md font-label-md text-on-surface-variant">
          محصول مرجوعی
        </legend>

        {order.items.map((item) => {
          const active = selected === item.productId;
          return (
            <label
              key={item.productId}
              className={cn(
                'flex cursor-pointer items-center gap-md rounded-lg border p-md transition-colors',
                active
                  ? 'border-primary bg-soft-mint/30'
                  : 'border-outline-variant hover:bg-surface-container-low',
              )}
            >
              <input
                type="radio"
                name="item"
                className="sr-only"
                checked={active}
                onChange={() => setSelected(item.productId)}
              />

              <span className="relative h-16 w-16 shrink-0 overflow-hidden rounded border border-outline-variant">
                <Image src={item.image} alt={item.title} fill sizes="64px" className="object-cover" />
              </span>

              <span className="flex min-w-0 flex-1 flex-col gap-xs">
                <span className="line-clamp-2 text-body-md text-on-surface">{item.title}</span>
                <span className="tabular text-caption text-on-surface-variant">
                  تعداد: {toPersianDigits(item.quantity)} عدد
                </span>
              </span>

              <Icon
                name={active ? 'check_circle' : 'radio_button_unchecked'}
                size={22}
                className={active ? 'text-primary' : 'text-outline-variant'}
              />
            </label>
          );
        })}

        {errors.item && <p className="text-caption text-error">{errors.item}</p>}
      </fieldset>

      <Select
        name="reason"
        label="دلیل مرجوعی"
        defaultValue=""
        required
        {...(errors.reason ? { error: errors.reason } : null)}
      >
        <option value="">انتخاب دلیل مرجوعی...</option>
        {returnReasons.map((reason) => (
          <option key={reason} value={reason}>
            {reason}
          </option>
        ))}
      </Select>

      <Textarea
        name="note"
        label="توضیحات تکمیلی"
        placeholder="توضیحات خود را بنویسید..."
        rows={5}
        {...(errors.note ? { error: errors.note } : null)}
      />

      {/* Photo evidence */}
      <div className="flex flex-col gap-sm">
        <span className="text-label-md font-label-md text-on-surface-variant">تصاویر محصول</span>

        {images.length > 0 && (
          <div className="flex flex-wrap gap-sm">
            {images.map((image) => (
              <span
                key={image.name}
                className="relative h-20 w-20 overflow-hidden rounded-lg border border-outline-variant"
              >
                {/* Blob preview: next/image cannot optimise object URLs. */}
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={image.url} alt={image.name} className="h-full w-full object-cover" />
                <button
                  type="button"
                  aria-label={`حذف ${image.name}`}
                  onClick={() => removeImage(image.name)}
                  className="absolute end-1 top-1 flex h-6 w-6 items-center justify-center rounded-full bg-inverse-surface/70 text-inverse-on-surface"
                >
                  <Icon name="close" size={16} />
                </button>
              </span>
            ))}
          </div>
        )}

        {images.length < MAX_IMAGES && (
          <label className="flex cursor-pointer items-center justify-center gap-sm rounded-lg border border-dashed border-outline-variant px-lg py-md text-label-md font-label-md text-primary transition-colors hover:bg-surface-container-low">
            <Icon name="add_a_photo" size={20} />
            افزودن تصویر محصول
            <input
              type="file"
              accept="image/jpeg,image/png"
              multiple
              className="sr-only"
              onChange={addImages}
            />
          </label>
        )}

        <span className="tabular text-caption text-outline">
          حداکثر {toPersianDigits(MAX_IMAGES)} تصویر با فرمت JPG یا PNG
        </span>
      </div>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="schedule" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          درخواست شما پس از بررسی شرایط مرجوعی توسط پشتیبانی بوژان پیگیری می‌شود. نتیجه طی ۲۴ ساعت
          کاری به اطلاع شما خواهد رسید.
        </p>
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

      {/* Sticky on mobile, inline on desktop — as drawn. */}
      <div className="glass-nav fixed inset-x-0 bottom-[72px] z-40 flex gap-md border-t border-outline-variant/40 px-margin-mobile py-md md:static md:border-0 md:bg-transparent md:p-0 md:backdrop-blur-none">
        <Button type="submit" size="lg" fullWidth loading={saving} className="md:w-auto md:px-xl">
          ثبت درخواست مرجوعی
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="lg"
          onClick={() => router.back()}
          className="md:w-auto md:px-xl"
        >
          انصراف
        </Button>
      </div>
    </form>
  );
}
