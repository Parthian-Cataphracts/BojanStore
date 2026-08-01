'use client';

import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { Button, Icon } from '@bojan/ui';
import { StatusScreen } from '@/components/status/StatusScreen';

/**
 * Screen 40 — Offline notice.
 *
 * Retry is a real connectivity check rather than a blind reload: if the browser
 * still reports no connection, telling the user so beats a failed navigation.
 */
export function OfflineNotice() {
  const router = useRouter();
  const [online, setOnline] = useState(true);
  const [checking, setChecking] = useState(false);

  useEffect(() => {
    setOnline(navigator.onLine);

    const update = () => setOnline(navigator.onLine);
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    return () => {
      window.removeEventListener('online', update);
      window.removeEventListener('offline', update);
    };
  }, []);

  function retry() {
    setChecking(true);
    if (navigator.onLine) {
      router.back();
      return;
    }
    // Still offline — drop the spinner so the message stays truthful.
    setTimeout(() => setChecking(false), 600);
  }

  return (
    <StatusScreen
      icon="wifi_off"
      tone="neutral"
      title="اتصال اینترنت برقرار نیست"
      message="برای ادامه خرید در بوژان، اتصال اینترنت خود را بررسی کنید و دوباره تلاش کنید."
      actions={
        <>
          <Button size="lg" fullWidth loading={checking} onClick={retry}>
            تلاش دوباره
          </Button>
          <Button variant="outline" size="lg" fullWidth onClick={() => router.back()}>
            بازگشت
          </Button>
        </>
      }
    >
      {online && (
        <p className="flex items-center gap-xs text-caption text-primary">
          <Icon name="wifi" size={16} />
          اتصال برقرار شد؛ می‌توانید ادامه دهید.
        </p>
      )}
    </StatusScreen>
  );
}
