import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, formatPrice } from '@bojan/ui';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { CheckoutOptionGroup } from '@/components/checkout/CheckoutOptionGroup';
import { WalletSplitNotice } from '@/components/checkout/WalletSplitNotice';
import { getPaymentMethods, getShippingMethods } from '@/lib/api/cart';
import { getWallet } from '@/lib/api/activity';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'انتخاب روش پرداخت',
  robots: { index: false },
};

/** Screen 75 — Choose a payment method. */
export default async function CheckoutPaymentPage() {
  const [shippingMethods, paymentMethods, wallet] = await Promise.all([
    getShippingMethods(),
    getPaymentMethods(),
    getWallet(),
  ]);

  return (
    <CheckoutShell
      step="payment"
      title="انتخاب روش پرداخت"
      showSummary
      extraRows={[{ label: 'هزینه ارسال', value: formatPrice(shippingMethods[0]!.price) }]}
      nextHref={routes.checkoutReview}
      nextLabel="بررسی نهایی سفارش"
      backHref={routes.checkoutDeliveryTime}
    >
      <CheckoutOptionGroup
        field="paymentMethodId"
        name="payment"
        options={paymentMethods.map((method) => ({
          id: method.id,
          title: method.label,
          // The wallet's own line carries the balance, which is per-shopper and
          // so cannot come from the method list.
          description: method.usesWallet ? `موجودی: ${formatPrice(wallet.balance)}` : method.note,
          icon: method.icon,
        }))}
      />

      <WalletSplitNotice
        balance={wallet.balance}
        walletMethodIds={paymentMethods.filter((m) => m.usesWallet).map((m) => m.id)}
        gatewayMethodIds={paymentMethods.filter((m) => m.requiresGateway).map((m) => m.id)}
      />

      <Link
        href={routes.checkoutCoupon}
        className="paper-card flex items-center justify-between gap-md rounded-lg p-lg transition-shadow hover:shadow-soft"
      >
        <span className="flex items-center gap-sm text-label-md font-semibold text-primary">
          <Icon name="sell" size={20} />
          کد تخفیف دارید؟
        </span>
        <Icon name="chevron_left" size={20} className="text-outline" />
      </Link>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="verified_user" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          اطلاعات کارت شما هرگز در سیستم بوژان ذخیره نمی‌شود و پرداخت مستقیماً روی درگاه بانک انجام
          می‌گیرد.
        </p>
      </Card>
    </CheckoutShell>
  );
}
