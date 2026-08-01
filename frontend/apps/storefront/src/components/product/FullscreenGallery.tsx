'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Icon, cn, toPersianDigits } from '@bojan/ui';
import { routes } from '@/lib/routes';

/**
 * Screen 83 — edge-to-edge image viewer with a glass control bar.
 * Arrow keys step through the set; in RTL, left means "next".
 */
export function FullscreenGallery({
  slug,
  title,
  alt,
  images,
}: {
  slug: string;
  title: string;
  alt: string;
  images: string[];
}) {
  const [index, setIndex] = useState(0);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === 'ArrowLeft') setIndex((i) => (i + 1) % images.length);
      if (event.key === 'ArrowRight') setIndex((i) => (i - 1 + images.length) % images.length);
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [images.length]);

  const current = images[index] ?? images[0];
  if (!current) return null;

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-inverse-surface">
      <Image
        src={current}
        alt={alt}
        fill
        priority
        sizes="100vw"
        className="object-contain"
      />

      {/* Top controls */}
      <div className="glass-panel relative z-10 flex items-center justify-between gap-md border-0 px-lg py-md">
        <Link
          href={routes.product(slug)}
          aria-label="بازگشت به محصول"
          className="text-primary transition-opacity hover:opacity-80"
        >
          <Icon name="arrow_forward" />
        </Link>

        <span className="tabular rounded-full bg-surface/80 px-md py-xs text-caption text-on-surface">
          {toPersianDigits(index + 1)} / {toPersianDigits(images.length)}
        </span>

        <button
          type="button"
          aria-label={saved ? 'حذف از علاقه‌مندی‌ها' : 'افزودن به علاقه‌مندی‌ها'}
          aria-pressed={saved}
          onClick={() => setSaved((value) => !value)}
          className={cn('transition-colors', saved ? 'text-secondary' : 'text-primary')}
        >
          <Icon name="favorite" filled={saved} />
        </button>
      </div>

      <div className="flex-1" />

      {/* Bottom strip */}
      <div className="glass-panel relative z-10 flex flex-col gap-md border-0 px-lg py-md pb-safe">
        <h1 className="text-body-md font-medium text-on-surface">{title}</h1>

        {images.length > 1 && (
          <div className="hide-scrollbar flex gap-sm overflow-x-auto">
            {images.map((image, position) => (
              <button
                key={image}
                type="button"
                onClick={() => setIndex(position)}
                aria-label={`تصویر ${toPersianDigits(position + 1)}`}
                aria-current={position === index}
                className={cn(
                  'relative h-16 w-16 shrink-0 overflow-hidden rounded-lg border-2 transition-colors',
                  position === index ? 'border-primary' : 'border-transparent opacity-70',
                )}
              >
                <Image src={image} alt="" fill sizes="64px" className="object-cover" />
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
