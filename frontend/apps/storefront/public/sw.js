/*
  The storefront's service worker.

  It exists for one thing: receiving push notifications. There is no caching and
  no offline behaviour here, deliberately — a worker that also serves pages is a
  worker that can serve a stale price, and this shop's prices are the whole
  point of it. Registering one narrows the scope of what can go wrong to what it
  is actually for.

  The payload arrives already decrypted: the browser opens it with the keys it
  minted when the customer subscribed, so what reaches this file is the plain
  object the API sealed.
*/

self.addEventListener('push', (event) => {
  // A push with no data is a wake-up from a service testing the subscription.
  // Showing nothing would be a silent notification, which some browsers
  // penalise the whole origin for, so it gets a generic one instead.
  let payload = { title: 'بوژان', body: 'خبر تازه‌ای دارید.', link: '/account/notifications' };

  if (event.data) {
    try {
      payload = { ...payload, ...event.data.json() };
    } catch {
      // Not JSON — treat the whole thing as the body rather than dropping it.
      payload = { ...payload, body: event.data.text() };
    }
  }

  event.waitUntil(
    self.registration.showNotification(payload.title, {
      body: payload.body,
      dir: 'rtl',
      lang: 'fa',
      // No `icon` or `badge`: naming a file that is not there is worse than
      // letting the browser draw its own default, which is what it does. Drop a
      // square PNG into `public/` and name it here when the shop has one.
      //
      // So a second message about the same thing replaces the first rather than
      // stacking. The link is the tag because two notifications pointing at one
      // screen are one piece of news.
      tag: payload.link || 'bojan',
      data: { link: payload.link || '/account/notifications' },
    }),
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const link = (event.notification.data && event.notification.data.link) || '/';
  const target = new URL(link, self.location.origin);

  // Same-origin only. The link comes from the shop's own API, but a worker that
  // opens whatever it is handed is one bad row away from being a redirector.
  if (target.origin !== self.location.origin) {
    return;
  }

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      // Focus a tab that is already on the shop rather than opening a fourth
      // one. Someone who clicks three notifications should end up with one tab.
      for (const client of clients) {
        if (client.url.startsWith(self.location.origin) && 'focus' in client) {
          client.navigate(target.href);
          return client.focus();
        }
      }

      return self.clients.openWindow(target.href);
    }),
  );
});

/*
  A subscription can be rotated by the push service without the customer doing
  anything. Without this the browser quietly stops receiving and the shop keeps
  sending to an endpoint nobody is listening at.
*/
self.addEventListener('pushsubscriptionchange', (event) => {
  event.waitUntil(
    (async () => {
      const subscription = event.newSubscription;
      if (!subscription) return;

      const keys = subscription.toJSON().keys;
      if (!keys) return;

      await fetch('/api/account/push-subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify({
          endpoint: subscription.endpoint,
          p256dh: keys.p256dh,
          auth: keys.auth,
        }),
      }).catch(() => {
        // Nobody is watching. The toggle re-registers on the next visit.
      });
    })(),
  );
});
