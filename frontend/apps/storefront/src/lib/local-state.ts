/**
 * Everything the storefront keeps in the browser, in one list.
 *
 * Four stores persist to `localStorage` and each owns its own key. Nothing
 * knew about all four at once, so signing out cleared the session cookie and
 * left the basket, the wishlist, the browsing history and the half-finished
 * checkout exactly where they were — on a shared machine the next person
 * inherited the last one's shopping, coupon included.
 *
 * The keys live here rather than being re-declared at the call site: a fifth
 * store added later is one line in this file, and forgetting to add it is a
 * visible omission rather than a silent one.
 */
export const LOCAL_STATE_KEYS = [
  'bojan.cart.v1',
  'bojan.wishlist.v1',
  'bojan.browsing.v1',
  'bojan.checkout.v1',
] as const;

/**
 * Drops every trace of the current shopper from this browser.
 *
 * The page is reloaded straight after — see `SignOutButton` — so the React
 * stores read empty storage on their next hydration rather than needing to be
 * told individually.
 */
export function forgetLocalShopper(): void {
  if (typeof window === 'undefined') return;

  try {
    for (const key of LOCAL_STATE_KEYS) {
      window.localStorage.removeItem(key);
    }
  } catch {
    // Blocked or unavailable storage (private mode, quota). There is nothing
    // to clear if there was nothing to write.
  }
}
