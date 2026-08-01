import type { Metadata } from 'next';
import Link from 'next/link';
import { Code, buttonClasses } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'درخواست سازمانی ثبت شد',
  robots: { index: false },
};

/** Screen 69 — Business request confirmation. */
export default async function BusinessSubmittedPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const code = first(params.code);

  return (
    <StatusScreen
      icon="check_circle"
      tone="success"
      title="درخواست شما با موفقیت ثبت شد"
      message="سپاس از اعتماد شما. کارشناسان ما به‌زودی با شما تماس خواهند گرفت."
      details={[
        ...(code ? [{ label: 'شماره پیگیری', value: <Code>{code}</Code> }] : []),
        { label: 'زمان تقریبی پاسخگویی', value: 'کمتر از ۲۴ ساعت کاری' },
        { label: 'واحد پیگیری', value: 'فروش سازمانی بوژان' },
      ]}
      actions={
        <>
          <Link
            href={routes.businessRequests}
            className={buttonClasses({ size: 'lg', fullWidth: true })}
          >
            پیگیری درخواست‌ها
          </Link>
          <Link
            href={routes.business}
            className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
          >
            بازگشت به خرید سازمانی
          </Link>
        </>
      }
      note="شماره پیگیری از طریق پیامک هم برای شما ارسال شد."
    />
  );
}
