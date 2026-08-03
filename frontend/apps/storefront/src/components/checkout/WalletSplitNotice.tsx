'use client';

import { Card, Icon, formatPrice } from '@bojan/ui';
import { useCart } from '@/lib/cart/store';
import { useCheckout } from '@/lib/checkout/store';

interface Props {
  /** The shopper's spendable balance, read on the server. */
  balance: number;
  /** Wire ids of the methods that draw on the wallet — `usesWallet` on the API's method list. */
  walletMethodIds: string[];
  /** Whether the chosen method can also collect a remainder through a gateway. */
  gatewayMethodIds: string[];
}

/**
 * What the wallet will actually pay, once the shopper has chosen to use it.
 *
 * The API takes the lesser of the balance and the bill and sends the rest to
 * the gateway. The arithmetic is repeated here only to say so on screen — the
 * server does not trust this, and neither should the shopper be made to work it
 * out from a balance and a total shown in different places.
 */
export function WalletSplitNotice({ balance, walletMethodIds, gatewayMethodIds }: Props) {
  const { selection, hydrated } = useCheckout();
  const { cart } = useCart();

  const chosen = selection.paymentMethodId;
  if (!hydrated || !chosen || !walletMethodIds.includes(chosen)) return null;

  const fromWallet = Math.min(balance, cart.total);
  const remainder = cart.total - fromWallet;
  const canCollectRemainder = gatewayMethodIds.includes(chosen);

  return (
    <Card className="flex flex-col gap-sm p-md">
      <p className="flex items-center gap-xs text-label-md font-semibold text-primary">
        <Icon name="account_balance_wallet" size={20} />
        پرداخت از کیف پول
      </p>

      <dl className="flex flex-col gap-xs text-body-md">
        <div className="flex items-center justify-between">
          <dt className="text-on-surface-variant">موجودی کیف پول</dt>
          <dd className="tabular text-on-surface">{formatPrice(balance)}</dd>
        </div>
        <div className="flex items-center justify-between">
          <dt className="text-on-surface-variant">از کیف پول کسر می‌شود</dt>
          <dd className="tabular font-label-md text-primary">{formatPrice(fromWallet)}</dd>
        </div>
        {remainder > 0 && (
          <div className="flex items-center justify-between border-t border-paper-border pt-xs">
            <dt className="text-on-surface-variant">باقیمانده قابل پرداخت</dt>
            <dd className="tabular font-label-md text-secondary">{formatPrice(remainder)}</dd>
          </div>
        )}
      </dl>

      {remainder === 0 ? (
        <p className="text-caption leading-relaxed text-on-surface-variant">
          موجودی کیف پول برای این سفارش کافی است و مبلغ دیگری پرداخت نمی‌کنید.
        </p>
      ) : canCollectRemainder ? (
        <p className="text-caption leading-relaxed text-on-surface-variant">
          موجودی کیف پول شما کمتر از مبلغ سفارش است. کل موجودی کسر می‌شود و باقیمانده
          را در درگاه بانکی پرداخت می‌کنید.
        </p>
      ) : (
        // Cash on delivery has no gateway behind it, so the API refuses an
        // order it cannot collect the shortfall for. Said here rather than left
        // to a rejected submit two screens later.
        <p
          role="alert"
          className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-caption leading-relaxed text-on-error-container"
        >
          <Icon name="error" size={18} className="mt-px shrink-0" />
          موجودی کیف پول برای این سفارش کافی نیست و با این روش پرداخت، امکان
          پرداخت باقیمانده وجود ندارد. روش دیگری را انتخاب کنید یا کیف پول را شارژ کنید.
        </p>
      )}
    </Card>
  );
}
