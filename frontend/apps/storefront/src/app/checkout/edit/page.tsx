import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getAddresses } from '@/lib/api/account';
import { getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تغییر اطلاعات سفارش',
  robots: { index: false },
};

/** Screen 80 — Edit order details before payment. */
export default async function CheckoutEditPage() {
  const shippingMethods = await getShippingMethods();
  const addresses = await getAddresses();
  const address = addresses.find((item) => item.isDefault) ?? addresses[0];
  // Not asserted. `getShippingMethods` answers `[]` when the API call fails, so
  // indexing it is a crash on the screen a shopper is on mid-purchase — the
  // same defect `ChosenShippingCard` was fixed for and these siblings were not.
  const shipping = shippingMethods[0];

  const sections = [
    {
      icon: 'place',
      title: 'آدرس تحویل',
      body: address ? `${address.province}، ${address.city}، ${address.line}` : '—',
      detail: address ? `گیرنده: ${address.recipient} — ${toPersianDigits(address.phone)}` : null,
      href: routes.checkoutAddress,
    },
    {
      icon: 'local_shipping',
      title: 'روش ارسال',
      body: shipping?.label ?? 'هنوز روش ارسالی انتخاب نشده است.',
      detail: shipping?.note ?? null,
      href: routes.checkoutShipping,
    },
    {
      icon: 'schedule',
      title: 'زمان تحویل',
      body: 'اولین بازه ممکن',
      detail: 'روز و بازه زمانی را می‌توانید تغییر دهید.',
      href: routes.checkoutDeliveryTime,
    },
    {
      icon: 'credit_card',
      title: 'روش پرداخت',
      body: 'پرداخت اینترنتی',
      detail: 'درگاه بانکی امن',
      href: routes.checkoutPayment,
    },
    {
      icon: 'sell',
      title: 'کد تخفیف',
      body: 'اعمال یا حذف کد تخفیف',
      detail: null,
      href: routes.checkoutCoupon,
    },
  ];

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="تغییر اطلاعات سفارش"
        backHref={routes.checkoutConfirm}
        subtitle="برای اعمال تغییرات، بخش مورد نظر را ویرایش کنید."
      />

      <Card className="flex items-start gap-sm border-secondary-container/50 bg-secondary-fixed/40 p-md">
        <Icon name="warning" size={20} className="mt-px shrink-0 text-secondary" />
        <p className="text-body-md leading-relaxed text-on-secondary-fixed-variant">
          توجه: تغییر در آدرس یا زمان ارسال ممکن است بر هزینه نهایی یا زمان تحویل سفارش شما تأثیر
          بگذارد.
        </p>
      </Card>

      <div className="grid gap-md md:grid-cols-2">
        {sections.map((section) => (
          <Card key={section.title} className="flex items-start justify-between gap-md p-lg">
            <div className="flex min-w-0 flex-col gap-xs">
              <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
                <Icon name={section.icon} size={20} />
                {section.title}
              </h2>
              <p className="text-body-md leading-relaxed text-on-surface">{section.body}</p>
              {section.detail && (
                <p className="tabular text-caption text-on-surface-variant">{section.detail}</p>
              )}
            </div>

            <Link
              href={section.href}
              className="shrink-0 text-label-md font-semibold text-secondary transition-colors hover:text-primary"
            >
              تغییر
            </Link>
          </Card>
        ))}
      </div>
    </Container>
  );
}
