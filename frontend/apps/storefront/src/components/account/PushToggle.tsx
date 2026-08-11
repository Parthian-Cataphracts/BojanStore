'use client';

import { useEffect, useState } from 'react';
import { Button, Card, Icon } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';

/**
 * What the browser will and will not let the shop do.
 *
 * `unsupported` covers browsers with no push at all and — the common case in
 * practice — pages not served over HTTPS, where the service worker API is
 * simply absent. `blocked` is a customer who said no, which cannot be undone
 * from a page: the browser deliberately refuses to ask twice, so the only
 * honest thing to show is where the setting lives.
 */
type State = 'loading' | 'unsupported' | 'blocked' | 'off' | 'on' | 'unavailable';

/** base64url → the Uint8Array `PushManager.subscribe` wants. */
function decodeKey(value: string): Uint8Array {
  const padded = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(
    value.length + ((4 - (value.length % 4)) % 4),
    '=',
  );

  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);

  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }

  return bytes;
}

/**
 * Switching browser notifications on for this device.
 *
 * Nothing here asks for permission on page load. A prompt a visitor did not ask
 * for is the fastest way to have it dismissed forever — browsers remember a
 * refusal and will not ask again — so the prompt only appears behind a button
 * the customer pressed.
 *
 * Per device rather than per account: this browser is what gets registered, so
 * the same person on a phone and a laptop switches it on twice, and switching
 * it off here does not silence the other one.
 */
export function PushToggle() {
  const [state, setState] = useState<State>('loading');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function read() {
      if (
        typeof window === 'undefined' ||
        !('serviceWorker' in navigator) ||
        !('PushManager' in window) ||
        !('Notification' in window)
      ) {
        if (!cancelled) setState('unsupported');
        return;
      }

      // Whether the shop has push configured at all. Asked before anything is
      // shown, so a shop that has not set it up shows nothing rather than a
      // control that fails when pressed.
      const available = await fetch('/api/push-availability', { cache: 'no-store' })
        .then((response) => (response.ok ? response.json() : { enabled: false }))
        .catch(() => ({ enabled: false }));

      if (cancelled) return;

      if (!available.enabled) {
        setState('unavailable');
        return;
      }

      if (Notification.permission === 'denied') {
        setState('blocked');
        return;
      }

      const registration = await navigator.serviceWorker.getRegistration();
      const existing = await registration?.pushManager.getSubscription();

      if (!cancelled) setState(existing ? 'on' : 'off');
    }

    void read();

    return () => {
      cancelled = true;
    };
  }, []);

  async function enable() {
    setBusy(true);
    setError(null);

    try {
      const available = await fetch('/api/push-availability', { cache: 'no-store' }).then(
        (response) => response.json(),
      );

      if (!available.enabled) {
        setState('unavailable');
        return;
      }

      // The prompt. Anything before this point is preparation; this is the one
      // line the customer sees a dialog for.
      const permission = await Notification.requestPermission();

      if (permission !== 'granted') {
        setState(permission === 'denied' ? 'blocked' : 'off');
        return;
      }

      const registration = await navigator.serviceWorker.register('/sw.js');
      await navigator.serviceWorker.ready;

      const subscription = await registration.pushManager.subscribe({
        // Required, and false is the only value browsers accept: it means every
        // message shows a notification. A silent push is what a page would use
        // to track someone in the background, and browsers revoke the
        // permission of origins that try.
        userVisibleOnly: true,
        applicationServerKey: decodeKey(available.publicKey),
      });

      const keys = subscription.toJSON().keys;

      if (!keys?.p256dh || !keys.auth) {
        throw new Error('missing keys');
      }

      await postJson('/api/account/push-subscribe', {
        endpoint: subscription.endpoint,
        p256dh: keys.p256dh,
        auth: keys.auth,
      });

      setState('on');
    } catch {
      setError('فعال‌سازی اعلان انجام نشد. دوباره تلاش کنید.');
    } finally {
      setBusy(false);
    }
  }

  async function disable() {
    setBusy(true);
    setError(null);

    try {
      const registration = await navigator.serviceWorker.getRegistration();
      const subscription = await registration?.pushManager.getSubscription();

      if (subscription) {
        // The shop is told first. Unsubscribing in the browser and then failing
        // to tell the server leaves a row the shop keeps sending to, and every
        // one of those is an HTTP request on every broadcast until the push
        // service reports it gone.
        await postJson('/api/account/push-unsubscribe', { endpoint: subscription.endpoint });
        await subscription.unsubscribe();
      }

      setState('off');
    } catch {
      setError('غیرفعال‌سازی اعلان انجام نشد. دوباره تلاش کنید.');
    } finally {
      setBusy(false);
    }
  }

  // Nothing at all while the answer is unknown, when the shop has push switched
  // off, or when the browser has none: an empty space is better than a control
  // that explains why it cannot work.
  if (state === 'loading' || state === 'unavailable' || state === 'unsupported') {
    return null;
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <div className="flex items-start gap-sm">
        <Icon name="notifications_active" size={22} className="mt-2xs shrink-0 text-primary" />

        <div className="flex min-w-0 flex-col gap-2xs">
          <h2 className="font-headline text-card-title text-on-surface">اعلان مرورگر</h2>
          <p className="text-caption leading-relaxed text-on-surface-variant">
            خبر سفارش‌ها و پیشنهادها را روی همین دستگاه دریافت کنید. برای هر دستگاه جداگانه
            تنظیم می‌شود.
          </p>
        </div>
      </div>

      {state === 'blocked' ? (
        <p
          role="status"
          className="rounded-lg bg-surface-container-low px-md py-sm text-body-md leading-relaxed text-on-surface-variant"
        >
          اعلان‌ها در تنظیمات مرورگر برای این سایت مسدود شده است. برای فعال‌سازی باید از تنظیمات
          خود مرورگر اجازه بدهید.
        </p>
      ) : (
        <div className="flex flex-wrap items-center gap-md">
          {state === 'on' ? (
            <Button variant="outline" icon="notifications_off" loading={busy} onClick={disable}>
              غیرفعال کردن
            </Button>
          ) : (
            <Button icon="notifications" loading={busy} onClick={enable}>
              فعال کردن اعلان
            </Button>
          )}

          {state === 'on' && !busy && (
            <span className="flex items-center gap-2xs text-caption text-primary">
              <Icon name="check_circle" size={18} />
              روی این دستگاه فعال است
            </span>
          )}
        </div>
      )}

      {error && (
        <p role="alert" className="flex items-start gap-2xs text-caption text-error">
          <Icon name="error" size={18} className="mt-px shrink-0" />
          {error}
        </p>
      )}
    </Card>
  );
}
