'use client';

import { useCallback, useState } from 'react';
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

  // A 404 the browser already has cached resolves before this element's
  // `onError` listener is even attached, so `complete && naturalWidth === 0`
  // right on mount is the only way to catch it — relying on `onError` alone
  // left the broken-image icon showing instead of the wordmark on every
  // reload once the 404 was cached.
  const checkLoaded = useCallback((node: HTMLImageElement | null) => {
    if (node?.complete && node.naturalWidth === 0) {
      setImageFailed(true);
    }
  }, []);

  if (imageFailed) {
    return <span className={wordmarkClassName}>{wordmark}</span>;
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element -- logo presence is unknown at build time, so this must probe at runtime rather than go through next/image's build-time optimizer.
    <img
      ref={checkLoaded}
      src={src}
      alt={wordmark}
      height={height}
      className={cn('w-auto object-contain', className)}
      style={{ height }}
      onError={() => setImageFailed(true)}
    />
  );
}
