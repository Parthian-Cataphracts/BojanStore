'use client';

import Image, { type ImageProps } from 'next/image';
import { useState } from 'react';
import { Icon, cn } from '@bojan/ui';

/**
 * A product photograph, with something to show when there is not one.
 *
 * The catalogue's images are remote, and remote images die — a URL that worked
 * when the product was created 404s a year later. `next/image` has no answer for
 * that: the browser falls back to painting the `alt` text, unstyled and at the
 * page's own font size, so a dead image turned a card into a wall of prose that
 * pushed the price and the title out of place. That is the worst of both, since
 * the shopper learns nothing about the product *and* loses the layout.
 *
 * So a failure renders a placeholder the size of the image that should have been
 * there. The description is still announced — it moves to screen-reader-only
 * text rather than being thrown away — and the card keeps its shape.
 */
export function ProductImage({
  src,
  alt,
  className,
  ...rest
}: Omit<ImageProps, 'src' | 'alt'> & { src: string | undefined | null; alt: string }) {
  const [failed, setFailed] = useState(false);

  if (!src || failed) {
    return (
      <span
        // Fills the same box `fill` would, so nothing around it moves.
        className="absolute inset-0 flex items-center justify-center bg-surface-container-highest text-outline"
        role="img"
        aria-label={alt}
      >
        <Icon name="image_not_supported" size={32} />
      </span>
    );
  }

  return (
    <Image
      src={src}
      alt={alt}
      className={cn(className)}
      onError={() => setFailed(true)}
      {...rest}
    />
  );
}
