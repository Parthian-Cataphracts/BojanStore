'use client';

import Image from 'next/image';
import { useState } from 'react';
import { cn } from '@bojan/ui';

/** Main image + thumbnail strip + pagination dots, per screen 06. */
export function ProductGallery({ images, alt }: { images: string[]; alt: string }) {
  const [active, setActive] = useState(0);
  const current = images[active] ?? images[0];

  if (!current) return null;

  return (
    <div className="flex flex-col gap-md">
      <div className="paper-card relative aspect-square w-full overflow-hidden rounded-xl">
        <Image
          src={current}
          alt={alt}
          fill
          priority
          sizes="(max-width: 768px) 100vw, 50vw"
          className="object-cover"
        />

        {images.length > 1 && (
          <div className="absolute bottom-md left-1/2 flex -translate-x-1/2 gap-xs">
            {images.map((image, index) => (
              <span
                key={image}
                className={cn(
                  'h-1.5 rounded-full transition-all',
                  index === active ? 'w-5 bg-primary' : 'w-1.5 bg-outline-variant',
                )}
              />
            ))}
          </div>
        )}
      </div>

      {images.length > 1 && (
        <div className="hide-scrollbar flex gap-sm overflow-x-auto">
          {images.map((image, index) => (
            <button
              key={image}
              type="button"
              onClick={() => setActive(index)}
              aria-label={`تصویر ${index + 1}`}
              aria-current={index === active}
              className={cn(
                'relative h-20 w-20 shrink-0 overflow-hidden rounded-lg border-2 transition-colors',
                index === active ? 'border-primary' : 'border-transparent hover:border-outline-variant',
              )}
            >
              <Image src={image} alt="" fill sizes="80px" className="object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
