'use client';

import { useState } from 'react';
import { Button, Card, Icon, cn, toPersianDigits } from '@bojan/ui';
import { mockAdminProducts } from '@/lib/mock';

interface Slot {
  id: string;
  url: string;
  alt: string;
}

const initial: Slot[] = mockAdminProducts.slice(0, 5).map((product, index) => ({
  id: `img-${index + 1}`,
  url: product.image,
  alt: product.title,
}));

/**
 * Screen 105 — Product images.
 * The first slot is the primary image; reordering promotes a different one.
 */
export function ProductImagesPanel() {
  const [slots, setSlots] = useState(initial);

  function move(index: number, direction: -1 | 1) {
    const target = index + direction;
    if (target < 0 || target >= slots.length) return;

    setSlots((current) => {
      const next = [...current];
      const [moved] = next.splice(index, 1);
      if (moved) next.splice(target, 0, moved);
      return next;
    });
  }

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-md p-lg">
        <label className="flex cursor-pointer flex-col items-center gap-xs rounded-lg border border-dashed border-outline-variant px-lg py-xl text-center transition-colors hover:bg-surface-container-low">
          <Icon name="add_photo_alternate" size={32} className="text-primary" />
          <span className="text-label-md font-semibold text-primary">افزودن تصویر</span>
          <span className="text-caption text-outline">
            JPG یا PNG، حداکثر ۳ مگابایت — حداقل ۱۰۰۰ در ۱۰۰۰ پیکسل
          </span>
          <input type="file" accept="image/jpeg,image/png" multiple className="sr-only" />
        </label>
      </Card>

      <div className="grid gap-md sm:grid-cols-2 lg:grid-cols-3">
        {slots.map((slot, index) => (
          <Card key={slot.id} className="flex flex-col gap-sm overflow-hidden">
            <div className="relative aspect-square w-full border-b border-paper-border bg-surface-container-highest">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={slot.url} alt={slot.alt} className="h-full w-full object-cover" />

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
                  onClick={() => setSlots((current) => current.filter((item) => item.id !== slot.id))}
                  className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
                >
                  <Icon name="delete" size={18} />
                </button>
              </div>
            </div>
          </Card>
        ))}
      </div>

      <div className="flex gap-md">
        <Button size="lg" className="px-xl">
          ذخیره ترتیب تصاویر
        </Button>
      </div>
    </div>
  );
}
