'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Icon } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { routes } from '@/lib/routes';

/**
 * Sign out, on screen 10.
 *
 * The session cookie is http-only, so clearing it has to happen server-side;
 * this posts to `/api/auth/logout` and then refreshes, which sends the
 * middleware's redirect back for every protected page the customer was on.
 */
export function SignOutButton() {
  const router = useRouter();
  const [pending, setPending] = useState(false);

  async function signOut() {
    setPending(true);
    try {
      await postJson('/api/auth/logout');
    } catch {
      // Either way the customer asked to leave — send them to the home page and
      // let the middleware decide what they can still see.
    }
    router.replace(routes.home);
    router.refresh();
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
