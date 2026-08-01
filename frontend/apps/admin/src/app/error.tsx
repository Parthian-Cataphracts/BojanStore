'use client';

import Link from 'next/link';
import { useEffect } from 'react';
import { Button, Card, Icon, buttonClasses } from '@bojan/ui';

/** Screen 160 — Admin error / connection failure. */
export default function AdminError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    // TODO: forward to the backend's logging endpoint once it exists.
    console.error(error);
  }, [error]);

  return (
    <div className="flex min-h-screen items-center justify-center p-lg">
      <Card className="flex max-w-md flex-col items-center gap-md p-xl text-center shadow-soft">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-surface-container-high text-primary">
          <Icon name="cloud_off" size={32} />
        </span>

        <h1 className="font-headline text-section-title text-primary">ارتباط برقرار نشد</h1>

        <p className="text-body-md leading-loose text-on-surface-variant">
          بارگذاری این بخش از پنل ممکن نشد. اتصال خود را بررسی کنید و دوباره تلاش کنید.
        </p>

        {error.digest && (
          <p className="latin text-caption text-outline">شناسه خطا: {error.digest}</p>
        )}

        <div className="mt-sm flex w-full flex-col gap-md sm:flex-row">
          <Button fullWidth onClick={reset}>
            تلاش دوباره
          </Button>
          <Link href="/" className={buttonClasses({ variant: 'outline', fullWidth: true })}>
            بازگشت به داشبورد
          </Link>
        </div>
      </Card>
    </div>
  );
}
