'use client';

import { useRouter } from 'next/navigation';
import { Icon, cn } from '@bojan/ui';

/**
 * The way out of a product page on a phone.
 *
 * The tab bar is not rendered on this screen — see `isImmersiveRoute` — so
 * without this the only way back is the browser's own gesture, and on a page
 * opened from a search result there is nothing behind it either.
 *
 * A labelled pill above the image rather than a bare arrow floating on it. An
 * icon alone on a photograph has to carry its own background to stay legible
 * and still leaves the shopper guessing; sat in the page with the word on it,
 * it reads at a glance and costs the picture nothing.
 *
 * `chevron_right` because the page is RTL: forward runs left, so back points
 * right. `PageHeader` picks `arrow_forward` by the same reasoning — the glyph
 * that points right, whatever it is called.
 */
export function ProductBackButton({
  fallbackHref,
  className,
}: {
  /** Where a page opened cold goes instead — its category listing. */
  fallbackHref: string;
  className?: string;
}) {
  const router = useRouter();

  function goBack() {
    /*
      A page opened from a search result, a shared link or a new tab has nothing
      behind it, and `router.back()` there either does nothing or leaves the
      shop. The length check is not exact — a tab that has been used has entries
      from elsewhere — but it is the difference between "there is somewhere to
      go back to" and "there is not", which is what this needs to know.
    */
    if (window.history.length > 1) router.back();
    else router.push(fallbackHref);
  }

  return (
    <button
      type="button"
      onClick={goBack}
      aria-label="بازگشت"
      className={cn(
        // `self-start`, because the page's column is a flex container and a
        // flex item stretches to the full width unless told not to —
        // `inline-flex` describes how the button lays its own children out and
        // says nothing about how wide it is asked to be. Without it the pill
        // ran the whole measure and read as a banner.
        'self-start',
        // Tighter on the icon's side than the label's, so the two look evenly
        // set rather than the glyph sitting adrift of the edge.
        'inline-flex h-9 items-center gap-xs rounded-full border border-outline-variant',
        'bg-surface-container-lowest ps-sm pe-md text-caption font-label-md text-on-surface',
        'transition-colors hover:bg-surface-container active:scale-95',
        className,
      )}
    >
      <Icon name="chevron_right" size={20} />
      بازگشت
    </button>
  );
}
