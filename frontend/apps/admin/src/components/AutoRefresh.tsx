'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

/**
 * Re-runs the server component it sits in, on an interval.
 *
 * For the live-chat screens, where the shopper's side polls every couple of
 * seconds and the operator's did not poll at all: a reply could be sent, but a
 * message arriving after the page loaded stayed invisible until the operator
 * thought to reload, which is not a console anyone can hold a conversation in.
 *
 * `router.refresh()` re-renders the server tree and reconciles it into the
 * page, so client state survives — an operator halfway through typing a reply
 * keeps their draft.
 *
 * It stops while the tab is hidden. An operator with the panel open in a
 * background tab should not be polling all afternoon.
 */
export function AutoRefresh({ seconds = 10 }: { seconds?: number }) {
  const router = useRouter();

  useEffect(() => {
    const tick = () => {
      if (document.visibilityState === 'visible') router.refresh();
    };

    const timer = window.setInterval(tick, seconds * 1000);
    // Catch up immediately on coming back, rather than waiting out the rest
    // of an interval that ran down while the tab was hidden.
    document.addEventListener('visibilitychange', tick);

    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', tick);
    };
  }, [router, seconds]);

  return null;
}
