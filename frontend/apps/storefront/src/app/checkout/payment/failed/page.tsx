import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'پرداخت ناموفق',
  robots: { index: false },
};

const reasons = [
  'قطع ارتباط با درگاه پرداخت',
  'کافی نبودن موجودی کارت',
  'انصراف از پرداخت توسط کاربر',
  'خطای موقت در شبکه بانکی',
];

/**
 * Screen 33 — Payment failed.
 *
 * Both buttons used to point at `/checkout`, which is a dead end by the time
 * anyone arrives here: the order was already placed before the shopper was
 * handed to the gateway, so the basket that screen reads is empty and going
 * back to it offers nothing to pay for. The order itself is what needs finding,
 * and it is sitting in the account, unpaid.
 *
 * The callback page passes the order number along when it knows one, so the
 * screen can name the order rather than sending the shopper to hunt for it in a
 * list. It is shown and nothing more — the order is looked up by the account
 * screens, which scope it to the customer themselves.
 */
export default async function PaymentFailedPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const orderNumber = first((await searchParams).order);

  return (
    <StatusScreen
      icon="error"
      tone="error"
      title="پرداخت انجام نشد"
      message={
        orderNumber
          ? `سفارش ${orderNumber} ثبت شده و در انتظار پرداخت است. اگر مبلغی از حساب شما کسر شده باشد، معمولاً به‌صورت خودکار بازگشت داده می‌شود.`
          : 'سفارش شما ثبت شده و در انتظار پرداخت است. اگر مبلغی از حساب شما کسر شده باشد، معمولاً به‌صورت خودکار بازگشت داده می‌شود.'
      }
      actions={
        <>
          <Link
            href={routes.orders}
            className={buttonClasses({ size: 'lg', fullWidth: true })}
          >
            مشاهده سفارش و تلاش دوباره
          </Link>
          <Link
            href={routes.home}
            className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
          >
            بازگشت به فروشگاه
          </Link>
        </>
      }
    >
      <Card className="w-full max-w-md p-lg text-start">
        <h2 className="mb-md flex items-center gap-sm text-label-md font-label-md text-primary">
          <Icon name="help" size={20} />
          ممکن است یکی از این موارد رخ داده باشد
        </h2>

        <ul className="flex flex-col gap-sm">
          {reasons.map((reason) => (
            <li key={reason} className="flex items-start gap-sm">
              <span
                aria-hidden="true"
                className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-outline"
              />
              <span className="text-body-md leading-relaxed text-on-surface-variant">{reason}</span>
            </li>
          ))}
        </ul>
      </Card>

      <Link
        href={routes.contact}
        className="inline-flex items-center gap-xs text-label-md font-label-md text-secondary transition-colors hover:text-primary"
      >
        نیاز به راهنمایی دارید؟ تماس با پشتیبانی
        <Icon name="support_agent" size={18} />
      </Link>
    </StatusScreen>
  );
}
