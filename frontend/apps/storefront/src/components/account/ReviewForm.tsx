'use client';

import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Textarea, cn, toPersianDigits } from '@bojan/ui';
import type { Product } from '@/lib/api/types';
import { routes } from '@/lib/routes';
import { formPayload, postJson } from '@/lib/api/submit';

/** Screen 56 — Write a product review. */
export function ReviewForm({ product }: { product: Product }) {
  const router = useRouter();
  const [rating, setRating] = useState(0);
  const [hovered, setHovered] = useState(0);
  const [errors, setErrors] = useState<{ rating?: string; body?: string }>({});
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Captured before the first await — React clears `currentTarget`
    // once the handler returns.
    const form = event.currentTarget;
    const body = String(new FormData(event.currentTarget).get('body') ?? '').trim();
    const next: typeof errors = {};

    if (rating === 0) next.rating = 'امتیاز خود را انتخاب کنید.';
    if (body.length < 10) next.body = 'دیدگاه خود را کامل‌تر بنویسید (حداقل ۱۰ نویسه).';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    try {
      await postJson('/api/account/review', { ...formPayload(form), productSlug: product.slug, rating });
      router.push(routes.reviews);
      router.refresh();
    } catch (cause) {
      setErrors({ body: cause instanceof Error ? cause.message : 'ثبت نظر انجام نشد.' });
      setSaving(false);
    }
  }

  // Hover preview beats the committed value while the pointer is over the stars.
  const shown = hovered || rating;

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-lg">
      <Card className="flex items-center gap-md p-lg">
        <span className="relative h-20 w-20 shrink-0 overflow-hidden rounded-lg border border-outline-variant">
          <Image
            src={product.image}
            alt={product.title}
            fill
            sizes="80px"
            className="object-cover"
          />
        </span>
        <div className="flex min-w-0 flex-col gap-xs">
          <h2 className="line-clamp-2 text-body-lg font-label-md text-primary">{product.title}</h2>
          <p className="text-caption text-on-surface-variant">
            نظر خود را درباره کیفیت و طراحی این محصول بنویسید
          </p>
        </div>
      </Card>

      <Card className="flex flex-col gap-lg p-lg">
        <fieldset className="flex flex-col gap-sm">
          <legend className="mb-sm text-label-md font-label-md text-on-surface-variant">
            امتیاز شما
          </legend>

          <div className="flex items-center gap-xs" onMouseLeave={() => setHovered(0)}>
            {[1, 2, 3, 4, 5].map((star) => (
              <button
                key={star}
                type="button"
                aria-label={`${toPersianDigits(star)} ستاره`}
                aria-pressed={rating === star}
                onClick={() => setRating(star)}
                onMouseEnter={() => setHovered(star)}
                className="transition-transform hover:scale-110"
              >
                <Icon
                  name="star"
                  filled={star <= shown}
                  size={34}
                  className={cn(star <= shown ? 'text-secondary-container' : 'text-outline-variant')}
                />
              </button>
            ))}

            {rating > 0 && (
              <span className="tabular ms-sm text-caption text-on-surface-variant">
                {toPersianDigits(rating)} از ۵
              </span>
            )}
          </div>

          {errors.rating && <p className="text-caption text-error">{errors.rating}</p>}
        </fieldset>

        <Textarea
          name="body"
          label="دیدگاه شما"
          placeholder="تجربه خود از این محصول را بنویسید..."
          rows={6}
          required
          {...(errors.body ? { error: errors.body } : null)}
        />

        <div className="flex flex-col gap-sm">
          <span className="text-label-md font-label-md text-on-surface-variant">تصاویر محصول</span>
          <label className="flex cursor-pointer flex-col items-center gap-xs rounded-lg border border-dashed border-outline-variant px-lg py-lg text-center transition-colors hover:bg-surface-container-low">
            <Icon name="add_photo_alternate" size={28} className="text-primary" />
            <span className="text-label-md font-label-md text-primary">افزودن تصویر (اختیاری)</span>
            <span className="text-caption text-outline">حداکثر حجم ۳ مگابایت (JPG, PNG)</span>
            <input type="file" accept="image/jpeg,image/png" multiple className="sr-only" />
          </label>
        </div>
      </Card>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="info" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          نظر شما پس از بررسی در صفحه محصول نمایش داده می‌شود. لطفاً از ادبیات محترمانه استفاده کنید
          و از درج اطلاعات شخصی خودداری نمایید.
        </p>
      </Card>

      <div className="flex flex-col gap-md sm:flex-row-reverse sm:justify-start">
        <Button type="submit" size="lg" loading={saving} className="sm:w-auto sm:px-xl">
          ارسال نظر
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="lg"
          onClick={() => router.back()}
          className="sm:w-auto sm:px-xl"
        >
          انصراف
        </Button>
      </div>
    </form>
  );
}
