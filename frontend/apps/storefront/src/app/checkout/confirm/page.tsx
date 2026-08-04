import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { ConfirmSummary } from '@/components/checkout/ConfirmSummary';
import { PlaceOrderButton } from '@/components/checkout/PlaceOrderButton';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getAddresses, getCurrentUser } from '@/lib/api/account';
import { getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تایید نهایی',
  robots: { index: false },
};

/** Screen 78 — Confirm details, then place the order. */
export default async function CheckoutConfirmPage() {
  const [user, shippingMethods, addresses] = await Promise.all([
    getCurrentUser(),
    getShippingMethods(),
    getAddresses(),
  ]);

  const fallbackAddressId = addresses.find((address) => address.isDefault)?.id ?? addresses[0]?.id;

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
      <ConfirmSummary phone={user.phone} shippingMethods={shippingMethods} />

      <Card className="flex items-start gap-sm p-md">
        <Icon name="lock" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          با زدن دکمه زیر به درگاه بانکی منتقل می‌شوید. اطلاعات کارت شما فقط روی صفحه بانک وارد
          می‌شود و بوژان به آن دسترسی ندارد.
        </p>
      </Card>

      <div className="flex flex-col gap-md sm:flex-row sm:items-start">
        {/*
          This used to link straight to the success screen, so the guided flow
          ended by telling the shopper their order was placed without placing
          one. The button posts the order and only then navigates.
        */}
        <div className="flex-1">
          <PlaceOrderButton {...(fallbackAddressId ? { fallbackAddressId } : null)} />
        </div>
        <Link
          href={routes.checkoutEdit}
          className={buttonClasses({ variant: 'outline', size: 'lg', className: 'flex-1' })}
        >
          تغییر اطلاعات سفارش
        </Link>
      </div>
    </Container>
  );
}
