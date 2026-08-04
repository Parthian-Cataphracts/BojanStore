import type { Metadata } from 'next';
import { Card, Icon } from '@bojan/ui';
import { EditableSelections } from '@/components/checkout/EditableSelections';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getAddresses } from '@/lib/api/account';
import { getPaymentMethods, getShippingMethods } from '@/lib/api/cart';
import { upcomingDeliveryDays } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تغییر اطلاعات سفارش',
  robots: { index: false },
};

/** Screen 80 — Edit order details before payment. */
export default async function CheckoutEditPage() {
  const [addresses, shippingMethods, paymentMethods] = await Promise.all([
    getAddresses(),
    getShippingMethods(),
    getPaymentMethods(),
  ]);

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

      <EditableSelections
        addresses={addresses}
        shippingMethods={shippingMethods}
        paymentMethods={paymentMethods}
        days={upcomingDeliveryDays(5)}
      />
    </Container>
  );
}
