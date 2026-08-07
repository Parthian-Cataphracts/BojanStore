'use client';

import { useCallback, useState } from 'react';
import { cn } from '../lib/cn';

export interface BrandLogoProps {
  /**
   * Path under `/public` to the image logo, e.g. `/logo.svg`.
   *
   * No default. It used to be `/logo.svg`, and no such file exists in either
   * app — so every page load in the whole shop requested one, and Next answered
   * each with a full 404 HTML document. A request per page view, on every page,
   * for a file nobody had put there.
   */
  src?: string;
  /** Text shown when no image logo is set, or the image fails to load. */
  wordmark: string;
  height?: number;
  className?: string;
  wordmarkClassName?: string;
}

/**
 * Renders the image logo at `src` if one loads, otherwise the text wordmark.
 *
 * Both apps pass `NEXT_PUBLIC_BRAND_LOGO`, which is unset until somebody has an
 * actual logo to serve — so the wordmark shows with no request attempted, and
 * turning the image on is dropping the file into `public/` and naming it in the
 * environment. The `onError` fallback stays for the case where the variable is
 * set and the file later is not.
 */
export function BrandLogo({ src, wordmark, height = 32, className, wordmarkClassName }: BrandLogoProps) {
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

  if (!src || imageFailed) {
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
