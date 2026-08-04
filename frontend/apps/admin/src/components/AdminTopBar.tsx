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
          'glass-header fixed inset-x-0 top-0 z-40 flex h-16 items-center justify-between border-b border-outline-variant px-lg transition-[inset-inline-start] duration-200',
          collapsed ? 'md:start-20' : 'md:start-64',
        )}
      >
        {/* Mobile: drawer button. Desktop: the sidebar is already on screen. */}
        <button
          type="button"
          aria-label="باز کردن منو"
          aria-expanded={drawerOpen}
          onClick={() => setDrawerOpen(true)}
          className="rounded-full p-2 text-primary transition-colors duration-200 hover:bg-surface-variant/50 active:scale-95 md:hidden"
        >
          <Icon name="menu" />
        </button>

        <h1 className="font-headline text-section-title text-primary max-md:hidden md:text-page-title">
          {title}
        </h1>

        {/* Mobile: the wordmark sits centre, per the phone drawings. */}
        <span className="font-headline text-headline-lg-mobile font-bold text-primary md:hidden">
          بوژان
        </span>

        <div className="flex items-center gap-lg">
          <form onSubmit={search} role="search" className="relative hidden lg:block">
            <Icon
              name="search"
              className="pointer-events-none absolute end-3 top-1/2 -translate-y-1/2 text-on-surface-variant"
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
              className="w-64 rounded-full border-none bg-surface-container-low py-2 pe-10 ps-4 text-body-md text-on-surface placeholder:text-outline focus:outline-none focus:ring-1 focus:ring-primary"
            />
          </form>

          <div className="flex items-center gap-sm">
            <button
              type="button"
              aria-label="اعلان‌ها"
              onClick={() => router.push('/campaigns/notifications')}
              className="flex h-10 w-10 items-center justify-center rounded-full text-on-surface-variant transition-colors hover:bg-surface-container hover:text-secondary"
            >
              <Icon name="notifications" />
            </button>
            <button
              type="button"
              aria-label="تنظیمات"
              onClick={() => router.push('/settings')}
              className="flex h-10 w-10 items-center justify-center rounded-full text-on-surface-variant transition-colors hover:bg-surface-container hover:text-secondary max-md:hidden"
            >
              <Icon name="settings_suggest" />
            </button>
          </div>

          <span className="h-8 w-px bg-outline-variant max-md:hidden" aria-hidden="true" />

          <div className="flex items-center gap-sm max-md:hidden">
            <span className="flex h-10 w-10 items-center justify-center rounded-full border border-outline-variant bg-soft-mint text-label-md font-label-md text-primary">
              م
            </span>
            <button
              type="button"
              onClick={signOut}
              disabled={signingOut}
              className="text-label-md font-label-md text-primary transition-colors hover:text-secondary disabled:opacity-50"
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
