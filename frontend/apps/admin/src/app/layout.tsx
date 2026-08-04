import type { Metadata, Viewport } from 'next';
import { inter, vazirmatn } from '@bojan/ui/fonts';
import { AdminShell } from '@/components/AdminShell';
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

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="fa" dir="rtl" className={`${vazirmatn.variable} ${inter.variable}`}>
      <head>
        {/* See the storefront layout: the icon font is otherwise not requested
            until globals.css has parsed, and the whole nav is icons. */}
        <link
          rel="preload"
          href="/fonts/material-symbols.woff2"
          as="font"
          type="font/woff2"
          crossOrigin="anonymous"
        />
      </head>
      <body className="min-h-screen bg-background">
        <AdminShell>{children}</AdminShell>
      </body>
    </html>
  );
}
