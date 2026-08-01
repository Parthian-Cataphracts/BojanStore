import type { Metadata, Viewport } from 'next';
import { inter, vazirmatn } from '@bojan/ui/fonts';
import { SiteHeader } from '@/components/layout/SiteHeader';
import { SiteFooter } from '@/components/layout/SiteFooter';
import { BottomNav } from '@/components/layout/BottomNav';
import { CartProvider } from '@/lib/cart/store';
import { CheckoutProvider } from '@/lib/checkout/store';
import { WishlistProvider } from '@/lib/wishlist/store';
import { BrowsingProvider } from '@/lib/browsing/store';
import { getCartSeed, getShippingFee } from '@/lib/api/cart';
import { getWishlistSeed } from '@/lib/api/account';
import { getBrowsingSeed } from '@/lib/api/activity';
import './globals.css';

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000'),
  title: {
    default: 'بوژان - برای لحظه‌های خلاق زندگی',
    template: '%s | بوژان',
  },
  description:
    'از نوشت‌افزار و دفتر تا ابزار هنری، معماری، هدیه و اکسسوری‌های خاص. فروشگاه اینترنتی بوژان.',
  openGraph: {
    type: 'website',
    locale: 'fa_IR',
    siteName: 'بوژان',
  },
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
      <body className="flex min-h-screen flex-col pb-[72px] md:pb-0">
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
                <SiteFooter />
                <BottomNav />
              </BrowsingProvider>
            </WishlistProvider>
          </CheckoutProvider>
        </CartProvider>
      </body>
    </html>
  );
}
