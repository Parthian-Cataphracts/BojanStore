'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Icon, cn } from '@bojan/ui';
import { AdminDrawer } from './AdminDrawer';
import { postJson } from '@/lib/submit';
import { useSidebarState } from '@/lib/sidebar-state';

/**
 * Fixed 64px top bar.
 *
 * Two bars, one component, split at `md` the way every other screen in this
 * project is: below `md` the phone drawings of screens 92-160 show a drawer
 * button, the wordmark and notifications; from `md` up, screen 92's desktop bar
 * clears the sidebar rail and carries the page title, search and the account
 * cluster.
 */
export function AdminTopBar({ title }: { title: string }) {
  const router = useRouter();
  const { collapsed } = useSidebarState();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [signingOut, setSigningOut] = useState(false);

  function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const term = new FormData(event.currentTarget).get('q');
    const trimmed = typeof term === 'string' ? term.trim() : '';

    router.push(trimmed ? `/products?q=${encodeURIComponent(trimmed)}` : '/products');
  }

  async function signOut() {
    setSigningOut(true);
    try {
      await postJson('/api/admin-auth/logout');
      router.replace('/login');
      router.refresh();
    } catch {
      // The cookie may already be gone; the middleware will bounce us anyway.
      router.replace('/login');
    }
  }

  return (
    <>
      <header
        className={cn(
          'glass-header border-outline-variant px-lg fixed inset-x-0 top-0 z-40 flex h-16 items-center justify-between border-b transition-[inset-inline-start] duration-200',
          collapsed ? 'md:start-20' : 'md:start-64',
        )}
      >
        {/* Mobile: drawer button. Desktop: the sidebar is already on screen. */}
        <button
          type="button"
          aria-label="باز کردن منو"
          aria-expanded={drawerOpen}
          onClick={() => setDrawerOpen(true)}
          className="text-primary hover:bg-surface-variant/50 rounded-full p-2 transition-colors duration-200 active:scale-95 md:hidden"
        >
          <Icon name="menu" />
        </button>

        {/*
          Chrome scale, not page scale. This was `text-page-title` — 40px — set
          inside a 64px bar, so the fixed furniture at the top of every screen
          shouted louder than the page under it and the two titles competed for
          the same job. The page's own heading is the one that should be read
          first; this is the label on the bar that says which page you are on.
        */}
        <h1 className="font-headline text-card-title text-primary max-md:hidden">{title}</h1>

        {/* Mobile: the wordmark sits centre, per the phone drawings. */}
        <span className="font-headline text-headline-lg-mobile text-primary font-bold md:hidden">
          بوژان
        </span>

        <div className="gap-lg flex items-center">
          <form onSubmit={search} role="search" className="relative hidden lg:block">
            {/*
              At the start of the field — the right — which is where `FilterBar`
              puts the same magnifier on every list screen in the panel. This
              one was on the other side, so the one search an operator uses from
              the chrome looked unlike the fifteen they use inside pages.
            */}
            <Icon
              name="search"
              className="text-on-surface-variant pointer-events-none absolute start-3 top-1/2 -translate-y-1/2"
            />
            {/*
              Submits to the product list's own `?q=`, which every list screen
              in the panel reads. It said "جستجو در پنل مدیریت" and searched
              nothing — there is no cross-section search endpoint to send it to,
              so it now names the catalogue it can actually search rather than
              promising a panel-wide one.
            */}
            <input
              type="search"
              name="q"
              placeholder="جستجوی محصولات"
              aria-label="جستجوی محصولات"
              className="bg-surface-container-low text-body-md text-on-surface placeholder:text-outline focus:ring-primary w-64 rounded-full border-none py-2 pe-4 ps-10 focus:outline-none focus:ring-1"
            />
          </form>

          <div className="gap-sm flex items-center">
            <button
              type="button"
              aria-label="اعلان‌ها"
              onClick={() => router.push('/campaigns/notifications')}
              className="text-on-surface-variant hover:bg-surface-container hover:text-secondary flex h-10 w-10 items-center justify-center rounded-full transition-colors"
            >
              <Icon name="notifications" />
            </button>
            <button
              type="button"
              aria-label="تنظیمات"
              onClick={() => router.push('/settings')}
              className="text-on-surface-variant hover:bg-surface-container hover:text-secondary flex h-10 w-10 items-center justify-center rounded-full transition-colors max-md:hidden"
            >
              <Icon name="settings_suggest" />
            </button>
          </div>

          <span className="bg-outline-variant h-8 w-px max-md:hidden" aria-hidden="true" />

          <div className="gap-sm flex items-center max-md:hidden">
            <span className="border-outline-variant bg-soft-mint text-label-md font-label-md text-primary flex h-10 w-10 items-center justify-center rounded-full border">
              م
            </span>
            <button
              type="button"
              onClick={signOut}
              disabled={signingOut}
              className="text-label-md font-label-md text-primary hover:text-secondary transition-colors disabled:opacity-50"
            >
              خروج
            </button>
          </div>
        </div>
      </header>

      <AdminDrawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        onSignOut={signOut}
        signingOut={signingOut}
      />
    </>
  );
}
