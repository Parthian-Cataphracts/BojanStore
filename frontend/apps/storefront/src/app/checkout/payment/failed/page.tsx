import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';
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

/** Screen 33 — Payment failed. */
export default function PaymentFailedPage() {
  return (
    <StatusScreen
      icon="error"
      tone="error"
      title="پرداخت انجام نشد"
      message="پرداخت شما تکمیل نشد. اگر مبلغی از حساب شما کسر شده باشد، معمولاً به‌صورت خودکار بازگشت داده می‌شود."
      actions={
        <>
          <Link
            href={routes.checkout}
            className={buttonClasses({ size: 'lg', fullWidth: true })}
          >
            تلاش دوباره برای پرداخت
          </Link>
          <Link
            href={routes.checkout}
            className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
          >
            انتخاب روش پرداخت دیگر
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
