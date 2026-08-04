'use client';

import { useState } from 'react';
import { cn } from '../lib/cn';

export interface BrandLogoProps {
  /** Path under `/public` to the image logo, e.g. `/logo.svg`. */
  src?: string;
  /** Text shown when no image logo is set, or the image fails to load. */
  wordmark: string;
  height?: number;
  className?: string;
  wordmarkClassName?: string;
}

/**
 * Renders the image logo dropped at `src` if one loads, otherwise the
 * existing text wordmark — so a project with no logo file yet (both apps,
 * today) keeps working unchanged, and dropping in `/logo.svg` (or
 * `/logo.png`) is the entire integration step, no code change required.
 */
export function BrandLogo({ src = '/logo.svg', wordmark, height = 32, className, wordmarkClassName }: BrandLogoProps) {
  const [imageFailed, setImageFailed] = useState(false);

  if (imageFailed) {
    return <span className={wordmarkClassName}>{wordmark}</span>;
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element -- logo presence is unknown at build time, so this must probe at runtime rather than go through next/image's build-time optimizer.
    <img
      src={src}
      alt={wordmark}
      height={height}
      className={cn('w-auto object-contain', className)}
      style={{ height }}
      onError={() => setImageFailed(true)}
    />
  );
}
