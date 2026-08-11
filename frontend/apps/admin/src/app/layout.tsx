import type { Metadata, Viewport } from 'next';
import { inter, vazirmatn } from '@bojan/ui/fonts';
import { AdminShell } from '@/components/AdminShell';
import { getAdminSession } from '@/lib/auth/server';
import './globals.css';

export const metadata: Metadata = {
  title: {
    default: 'پنل مدیریت بوژان',
    template: '%s | پنل مدیریت بوژان',
  },
  // The admin panel must never be indexed.
  robots: { index: false, follow: false },
};

export const viewport: Viewport = {
  themeColor: '#003441',
  width: 'device-width',
  initialScale: 1,
};

// Every screen reads per-operator, role-scoped data — none of it is safe to
// prerender or cache across requests, unlike the storefront's public catalogue.
export const dynamic = 'force-dynamic';

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  /*
    Read here rather than per screen: the navigation needs the role to say which
    sections this operator may actually open, and the navigation is rendered by
    the shell, above every page. `getAdminSession` rather than `require*` — this
    layout also wraps the sign-in screen, where there is no session yet and a
    redirect would be a loop.
  */
  const session = await getAdminSession();

  return (
    <html lang="fa" dir="rtl" className={`${vazirmatn.variable} ${inter.variable}`}>
      <head>
        {/* See the storefront layout: the icon font is otherwise not requested
            until globals.css has parsed, and the whole nav is icons. */}
        <link
          rel="preload"
          href="/fonts/material-symbols.woff2?v=a2948a6d"
          as="font"
          type="font/woff2"
          crossOrigin="anonymous"
        />
      </head>
      {/*
        Browser extensions write their own attributes onto <body> before React
        hydrates — QuillBot's `data-qb-installed`, password managers, translation
        tools — and every one of them reads to React as the server and client
        disagreeing about this element. Nothing here can stop them, so the
        mismatch on <body> itself is declared expected.

        Scoped to this one element on purpose: `suppressHydrationWarning` covers
        the element's own attributes and text, not its subtree, so a real
        mismatch anywhere inside the panel is still reported.
      */}
      <body className="min-h-screen bg-background" suppressHydrationWarning>
        <AdminShell role={session?.role ?? null}>{children}</AdminShell>
      </body>
    </html>
  );
}
