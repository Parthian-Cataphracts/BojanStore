'use client';

import { useState } from 'react';
import { Icon } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { forgetLocalShopper } from '@/lib/local-state';
import { routes } from '@/lib/routes';

/**
 * Sign out, on screen 10.
 *
 * The session cookie is http-only, so clearing it has to happen server-side;
 * this posts to `/api/auth/logout` and then refreshes, which sends the
 * middleware's redirect back for every protected page the customer was on.
 *
 * The cookie was the only thing that used to go. Everything the shop keeps in
 * the browser — the basket and its coupon, the wishlist, what was viewed and
 * searched for, the half-finished checkout — stayed exactly where it was, so on
 * a shared machine the next person to open the site inherited the last one's
 * shopping. Signing out has to mean the browser forgets too.
 */
export function SignOutButton() {
  const [pending, setPending] = useState(false);

  async function signOut() {
    setPending(true);
    try {
      await postJson('/api/auth/logout');
    } catch {
      // Either way the customer asked to leave — send them to the home page and
      // let the middleware decide what they can still see.
    }

    forgetLocalShopper();

    // A full navigation, not `router.replace` — the cart, wishlist and
    // browsing providers hold their state in memory and write it back on
    // change, so a client-side transition would repopulate the storage that was
    // just cleared. Reloading makes every provider hydrate from empty.
    window.location.assign(routes.home);
  }

  return (
    <button
      type="button"
      onClick={signOut}
      disabled={pending}
      className="flex items-center justify-center gap-sm rounded-lg border border-error/40 px-lg py-md text-label-md font-label-md text-error transition-colors hover:bg-error-container disabled:opacity-50"
    >
      <Icon name="logout" size={20} />
      خروج از حساب کاربری
    </button>
  );
}
