import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';

export const metadata: Metadata = { title: 'عدم دسترسی' };

/** Screen 158 — Access denied. */
export default function ForbiddenPage() {
  return (
    <div className="flex min-h-screen items-center justify-center p-lg">
      <Card className="flex max-w-md flex-col items-center gap-md p-xl text-center shadow-soft">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-error-container text-on-error-container">
          <Icon name="lock" size={32} />
        </span>

        <h1 className="font-headline text-section-title text-primary">دسترسی شما به این بخش محدود است</h1>

        <p className="text-body-md leading-loose text-on-surface-variant">
          نقش کاربری شما اجازه مشاهده این صفحه را ندارد. اگر فکر می‌کنید اشتباهی رخ داده، با مالک
          فروشگاه تماس بگیرید.
        </p>

        <div className="mt-sm flex w-full flex-col gap-md sm:flex-row">
          <Link href="/" className={buttonClasses({ fullWidth: true })}>
            بازگشت به داشبورد
          </Link>
          <Link
            href="/settings/users"
            className={buttonClasses({ variant: 'outline', fullWidth: true })}
          >
            مشاهده نقش‌ها
          </Link>
        </div>
      </Card>
    </div>
  );
}
