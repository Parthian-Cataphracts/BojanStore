import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { ConfirmSummary } from '@/components/checkout/ConfirmSummary';
import { PlaceOrderButton } from '@/components/checkout/PlaceOrderButton';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getCurrentUser } from '@/lib/api/account';
import { getShippingMethods } from '@/lib/api/cart';
import { getStoreSettings } from '@/lib/api/store';
import { routes } from '@/lib/routes';

/*
 * Rendered on request, not at build.
 *
 * This page reads the catalogue, and the catalogue lives behind the API — which
 * does not exist when the image is built. Prerendering it meant `next build`
 * fetching from a host that is not up yet, which is exactly how the Docker
 * build failed. The alternative, emitting it with whatever an unreachable API
 * returns, is worse: the first visitors after a deploy would be served an empty
 * shop until the first revalidation filled it in.
 *
 * Nothing is lost by it. The fetches underneath already declare their own
 * `revalidate` window, so the API is not called per request either way — the
 * caching just happens a layer down, where stock and prices can expire on their
 * own schedule instead of being frozen into the image.
 */
export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'تایید نهایی',
  robots: { index: false },
};

/** Screen 78 — Confirm details before handing off to the payment gateway. */
export default async function CheckoutConfirmPage() {
  const shippingMethods = await getShippingMethods();
  const { promises } = await getStoreSettings();
  const user = await getCurrentUser();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="تایید نهایی" backHref={routes.checkoutReview} />

      <Card className="flex items-start gap-md p-lg">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="security" size={24} />
        </span>
        <div className="flex flex-col gap-xs">
          <h2 className="text-body-lg font-semibold text-primary">اطلاعات پرداخت</h2>
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            لطفاً پیش از انتقال به درگاه بانکی، اطلاعات را بررسی نمایید.
          </p>
        </div>
      </Card>

      {/*
        No order number here. One is assigned when the order is placed, and the
        "#ORD-9482" this used to show was a constant — the same "temporary"
        reference for every shopper, on the screen where they are asked to check
        their details before paying.
      */}
      <ConfirmSummary
        phone={user.phone}
        shippingMethods={shippingMethods}
        freeShippingThreshold={promises.freeShippingThreshold}
      />

      <Card className="flex items-start gap-sm p-md">
        <Icon name="lock" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          با زدن دکمه زیر به درگاه بانکی منتقل می‌شوید. اطلاعات کارت شما فقط روی صفحه بانک وارد
          می‌شود و بوژان به آن دسترسی ندارد.
        </p>
      </Card>

      {/*
        This was a link straight to the success screen: the guided flow
        collected an address, a shipping method, a delivery window and a payment
        method across four screens and then congratulated the shopper on an
        order that was never placed. The button posts the same body screen 08
        does, to the same route.
      */}
      <div className="flex flex-col gap-md sm:flex-row">
        <div className="flex-1">
          <PlaceOrderButton />
        </div>
        <Link
          href={routes.checkoutEdit}
          className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
        >
          تغییر اطلاعات سفارش
        </Link>
      </div>
    </Container>
  );
}
