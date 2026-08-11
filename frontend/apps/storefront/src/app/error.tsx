'use client';

import Link from 'next/link';
import { useEffect } from 'react';
import { Button, Icon, buttonClasses } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';
import { routes } from '@/lib/routes';

/**
 * Screen 39 — Server error.
 *
 * Next.js renders this for any uncaught error in a route segment. It has to be
 * a client component and cannot use the shared layout's server data.
 */
export default function GlobalError({
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
    <StatusScreen
      icon="cloud_off"
      tone="neutral"
      title="مشکلی پیش آمد"
      message="در حال حاضر امکان نمایش این بخش وجود ندارد. لطفاً چند لحظه دیگر دوباره تلاش کنید."
      actions={
        <>
          <Button size="lg" fullWidth onClick={reset}>
            تلاش دوباره
          </Button>
          <Link
            href={routes.contact}
            className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
          >
            تماس با پشتیبانی
          </Link>
        </>
      }
    >
      {/* The reference support needs to find this in the logs. Without it the
          report is "it didn't work", which is not something anyone can look up. */}
      {error.digest && (
        <p className="text-caption text-on-surface-variant">
          کد پیگیری برای پشتیبانی: <code className="latin">{error.digest}</code>
        </p>
      )}

      <p className="flex items-center gap-xs text-caption text-on-surface-variant">
        <Icon name="info" size={16} />
        تیم بوژان در حال بررسی مشکل است.
      </p>
    </StatusScreen>
  );
}
