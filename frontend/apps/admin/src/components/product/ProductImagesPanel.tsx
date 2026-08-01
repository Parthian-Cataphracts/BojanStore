'use client';

import { useState, type ChangeEvent } from 'react';
import { Button, Card, Icon, cn, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/submit';

/**
 * Screen 105 — Product images.
 *
 * The first slot is the primary image; reordering promotes a different one.
 * The whole list is posted back as `images`, primary first, which is the shape
 * `POST /products` reads — so a removal here is a removal there.
 *
 * It previously rendered five pictures borrowed from the mock catalogue,
 * whichever product was open, and neither the picker nor the save button did
 * anything. Both now do, and what is shown is the product's own gallery.
 */
export function ProductImagesPanel({
  productId,
  images,
}: {
  productId: string;
  images: readonly string[];
}) {
  const [slots, setSlots] = useState<string[]>([...images]);
  const [uploading, setUploading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function move(index: number, direction: -1 | 1) {
    const target = index + direction;
    if (target < 0 || target >= slots.length) return;

    setSaved(false);
    setSlots((current) => {
      const next = [...current];
      const [moved] = next.splice(index, 1);
      if (moved) next.splice(target, 0, moved);
      return next;
    });
  }

  async function addImages(event: ChangeEvent<HTMLInputElement>) {
    const files = [...(event.target.files ?? [])];
    // Reset first so picking the same file again still fires a change.
    event.target.value = '';
    if (files.length === 0) return;

    setError(null);
    setUploading(true);
    try {
      // Sequential rather than parallel: the upload route is rate limited per
      // operator, and a burst of a dozen would spend that window at once.
      const uploaded: string[] = [];
      for (const file of files) {
        const body = new FormData();
        body.append('file', file);

        const response = await fetch('/api/upload/products', { method: 'POST', body });
        const result = (await response.json().catch(() => null)) as
          | { url?: string; error?: string }
          | null;

        if (!response.ok || !result?.url) {
          throw new Error(result?.error ?? 'بارگذاری تصویر انجام نشد.');
        }
        uploaded.push(result.url);
      }

      setSlots((current) => [...current, ...uploaded]);
      setSaved(false);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'بارگذاری تصویر انجام نشد.');
    } finally {
      setUploading(false);
    }
  }

  async function save() {
    setSaving(true);
    setError(null);
    try {
      await postJson('/api/admin/products', { id: productId, images: slots });
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره تصاویر انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-md p-lg">
        <label className="flex cursor-pointer flex-col items-center gap-xs rounded-lg border border-dashed border-outline-variant px-lg py-xl text-center transition-colors hover:bg-surface-container-low">
          <Icon
            name={uploading ? 'progress_activity' : 'add_photo_alternate'}
            size={32}
            className="text-primary"
          />
          <span className="text-label-md font-semibold text-primary">
            {uploading ? 'در حال بارگذاری…' : 'افزودن تصویر'}
          </span>
          <span className="text-caption text-outline">
            JPG، PNG یا WebP — حداکثر ۸ مگابایت
          </span>
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            multiple
            disabled={uploading}
            onChange={addImages}
            className="sr-only"
          />
        </label>
      </Card>

      {slots.length === 0 ? (
        <Card className="flex flex-col items-center gap-xs p-xl text-center">
          <Icon name="image" size={32} className="text-outline" />
          <p className="text-body-md text-on-surface-variant">
            هنوز تصویری برای این محصول ثبت نشده است.
          </p>
        </Card>
      ) : (
        <div className="grid gap-md sm:grid-cols-2 lg:grid-cols-3">
          {slots.map((url, index) => (
            <Card key={url} className="flex flex-col gap-sm overflow-hidden">
              <div className="relative aspect-square w-full border-b border-paper-border bg-surface-container-highest">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={url} alt="" className="h-full w-full object-cover" />

                {index === 0 && (
                  <span className="absolute end-sm top-sm rounded-full bg-primary px-md py-xs text-caption font-semibold text-on-primary">
                    تصویر اصلی
                  </span>
                )}
              </div>

              <div className="flex items-center justify-between gap-sm p-md">
                <span className="tabular text-caption text-on-surface-variant">
                  ترتیب {toPersianDigits(index + 1)}
                </span>

                <div className="flex items-center gap-xs">
                  <button
                    type="button"
                    aria-label="انتقال به جلو"
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                    className={cn(
                      'rounded p-xs text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary',
                      index === 0 && 'cursor-not-allowed opacity-40',
                    )}
                  >
                    <Icon name="arrow_forward" size={18} />
                  </button>
                  <button
                    type="button"
                    aria-label="انتقال به عقب"
                    disabled={index === slots.length - 1}
                    onClick={() => move(index, 1)}
                    className={cn(
                      'rounded p-xs text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary',
                      index === slots.length - 1 && 'cursor-not-allowed opacity-40',
                    )}
                  >
                    <Icon name="arrow_back" size={18} />
                  </button>
                  <button
                    type="button"
                    aria-label="حذف تصویر"
                    onClick={() => {
                      setSaved(false);
                      setSlots((current) => current.filter((item) => item !== url));
                    }}
                    className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
                  >
                    <Icon name="delete" size={18} />
                  </button>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      <div className="flex flex-wrap items-center gap-md">
        <Button
          size="lg"
          loading={saving}
          disabled={slots.length === 0}
          onClick={save}
          className="px-xl"
        >
          ذخیره ترتیب تصاویر
        </Button>

        {saved && (
          <span className="flex items-center gap-xs text-caption text-primary">
            <Icon name="check_circle" size={16} />
            تصاویر ذخیره شد.
          </span>
        )}

        {error && (
          <span role="alert" className="flex items-center gap-xs text-caption text-error">
            <Icon name="error" size={16} />
            {error}
          </span>
        )}
      </div>
    </div>
  );
}
