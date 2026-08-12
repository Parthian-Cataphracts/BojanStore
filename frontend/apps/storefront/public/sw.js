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

  event.waitUntil(openTarget(target.href));
});

/*
  Focus a tab that is already on the shop rather than opening a fourth one.
  Someone who clicks three notifications should end up with one tab.

  `navigate` is awaited, and a refusal falls through to opening a window. The
  match deliberately includes uncontrolled clients — a tab that was already open
  when the customer switched notifications on is not controlled by this worker
  until it reloads — and `navigate` rejects for precisely those. Calling it
  without waiting focused that tab and dropped the link on the floor, so someone
  tapping "your order has shipped" was handed whatever they had been reading.
*/
async function openTarget(href) {
  const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });

  for (const client of clients) {
    if (!client.url.startsWith(self.location.origin) || !('focus' in client)) {
      continue;
    }

    try {
      await client.navigate(href);
      return client.focus();
    } catch {
      // This one cannot be steered. Another may be, and failing that a new
      // window certainly can.
    }
  }

  return self.clients.openWindow(href);
}

/*
  A subscription can be rotated by the push service without the customer doing
  anything. Without this the browser quietly stops receiving and the shop keeps
  sending to an endpoint nobody is listening at.

  The event is specified to carry the replacement in `newSubscription`, and
  Chrome — where most of this shop's customers are — never populates it. Reading
  that field and giving up when it was empty meant this handler did nothing at
  all on the browser it mattered most on: the rotation it exists to survive went
  unhandled, which is the silence described above plus a request per broadcast
  to an endpoint nobody answers. When the field is missing the worker subscribes
  again itself, which is what every browser will do.
*/
self.addEventListener('pushsubscriptionchange', (event) => {
  event.waitUntil(reregister(event));
});

/** base64url → the Uint8Array `PushManager.subscribe` wants. */
function decodeKey(value) {
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

/*
  The key to subscribe against. The expiring subscription carries the one it was
  created with, which is both cheaper and more accurate than asking the shop —
  it is the key this browser is already registered under. Only when the browser
  hands over no old subscription at all is the API asked.
*/
async function applicationServerKey(old) {
  const carried = old && old.options && old.options.applicationServerKey;
  if (carried) return carried;

  const available = await fetch('/api/push-availability', { cache: 'no-store' })
    .then((response) => (response.ok ? response.json() : null))
    .catch(() => null);

  return available && available.enabled && available.publicKey
    ? decodeKey(available.publicKey)
    : null;
}

async function reregister(event) {
  const old = event.oldSubscription;
  let subscription = event.newSubscription;

  if (!subscription) {
    const key = await applicationServerKey(old);
    if (!key) return;

    try {
      subscription = await self.registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: key,
      });
    } catch {
      // Permission withdrawn, or the push service refused. Nothing here can
      // recover that; the toggle re-registers on the next visit.
      return;
    }
  }

  const keys = subscription.toJSON().keys;
  if (!keys) return;

  await post('/api/account/push-subscribe', {
    endpoint: subscription.endpoint,
    p256dh: keys.p256dh,
    auth: keys.auth,
  });

  // The dead row does not remove itself. Registering the new endpoint adds a
  // second one beside it, and the shop pays for the old on every broadcast
  // until the push service admits it is gone — so it is retired here. Safe when
  // the shop has already forgotten it: nothing to forget is a success.
  if (old && old.endpoint && old.endpoint !== subscription.endpoint) {
    await post('/api/account/push-unsubscribe', { endpoint: old.endpoint });
  }
}

function post(path, body) {
  return fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'same-origin',
    body: JSON.stringify(body),
  }).catch(() => {
    // Nobody is watching. The toggle re-registers on the next visit.
  });
}
