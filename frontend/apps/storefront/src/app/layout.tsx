import type { Metadata, Viewport } from 'next';
import { inter, vazirmatn } from '@bojan/ui/fonts';
import { SiteHeader } from '@/components/layout/SiteHeader';
import { ShopChrome } from '@/components/layout/ShopChrome';
import { SiteFooter } from '@/components/layout/SiteFooter';
import { ChatWidget } from '@/components/chat/ChatWidget';
import { CartProvider } from '@/lib/cart/store';
import { CheckoutProvider } from '@/lib/checkout/store';
import { WishlistProvider } from '@/lib/wishlist/store';
import { BrowsingProvider } from '@/lib/browsing/store';
import { getCartSeed, getShippingFee } from '@/lib/api/cart';
import { getWishlistSeed } from '@/lib/api/account';
import { getBrowsingSeed } from '@/lib/api/activity';
import { siteUrl } from '@/lib/seo';
import './globals.css';

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: {
    default: 'بوژان - برای لحظه‌های خلاق زندگی',
    template: '%s | بوژان',
  },
  description:
    'از نوشت‌افزار و دفتر تا ابزار هنری، معماری، هدیه و اکسسوری‌های خاص. فروشگاه اینترنتی بوژان.',
  /*
   * A self-referencing canonical for every page that does not set its own.
   * Only two of the eighty-nine did, so a link carrying `?utm_source=...` was
   * an independently indexable duplicate of whatever it pointed at, as was
   * every filter and sort combination on the listing. Pages with a canonical of
   * their own — the product and category details — still override this.
   */
  alternates: { canonical: './' },
  openGraph: {
    type: 'website',
    locale: 'fa_IR',
    siteName: 'بوژان',
    /*
     * A default share image for every page that does not have one of its own.
     * There was none at all, so a link to the shop posted anywhere — Telegram,
     * WhatsApp, Twitter, a Slack channel — rendered as a bare line of text
     * beside every competitor's card. Product pages still override this with
     * the product's own photo, which is the better picture when there is one.
     */
    images: [{ url: '/og-default.png', width: 1200, height: 630, alt: 'بوژان — برای لحظه‌های خلاق زندگی' }],
  },
  // Twitter reads the Open Graph tags when these are absent, but `summary_large_image`
  // is what makes it render the 1200×630 card rather than a thumbnail beside the text.
  twitter: { card: 'summary_large_image' },
};

export const viewport: Viewport = {
  themeColor: '#003441',
  width: 'device-width',
  initialScale: 1,
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  // Shipping and the demo basket are resolved on the server so the cart's
  // numbers come from the data layer rather than from constants in client code.
  const [shipping, seed, savedSeed, browsingSeed] = await Promise.all([
    getShippingFee(),
    getCartSeed(),
    getWishlistSeed(),
    getBrowsingSeed(),
  ]);

  return (
    <html lang="fa" dir="rtl" className={`${vazirmatn.variable} ${inter.variable}`}>
      <head>
        {/*
          The icon font is named only by `@font-face` in globals.css, so without
          this the browser cannot even ask for it until the stylesheet has
          downloaded and parsed — and every icon in the header is waiting on it.
          `next/font` already preloads the two text faces this way.
        */}
        <link
          rel="preload"
          href="/fonts/material-symbols.woff2?v=f6697bec"
          as="font"
          type="font/woff2"
          crossOrigin="anonymous"
        />
      </head>
      {/* See the admin layout: extensions write their own attributes onto
          <body> before React hydrates, and the mismatch that causes is not
          something the app can prevent. Scoped to this element only, so a real
          mismatch inside the page is still reported. */}
      <body className="min-h-viewport flex flex-col" suppressHydrationWarning>
        <CartProvider shipping={shipping} {...(seed ? { seed } : null)}>
          {/* The guided checkout's selections, so a choice made on one step
              survives the navigation to the next. */}
          <CheckoutProvider>
            <WishlistProvider {...(savedSeed ? { seed: savedSeed } : null)}>
              <BrowsingProvider
                {...(browsingSeed ? { seedViewed: browsingSeed.viewed } : null)}
                {...(browsingSeed ? { seedTerms: browsingSeed.terms } : null)}
              >
                {/*
                  Keyboard users otherwise tab through the whole nav on every
                  page before reaching anything. Visible only when focused.
                */}
                <a
                  href="#main"
                  className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:z-[60] focus:rounded-lg focus:bg-primary focus:px-lg focus:py-md focus:text-label-md focus:text-on-primary focus:start-2"
                >
                  پرش به محتوای اصلی
                </a>

                <SiteHeader />
                <main id="main" className="flex-1">
                  {children}
                </main>
                <ShopChrome footer={<SiteFooter />} />
                <ChatWidget />
              </BrowsingProvider>
            </WishlistProvider>
          </CheckoutProvider>
        </CartProvider>
      </body>
    </html>
  );
}
