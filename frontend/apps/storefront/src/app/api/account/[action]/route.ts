import { NextResponse } from 'next/server';
import { getSession } from '@/lib/auth/server';
import { problemMessage } from '@/lib/api/problem';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import { api, useMockData } from '@/lib/api/client';

/**
 * Customer-initiated writes — the profile, addresses, reviews, questions,
 * returns, stock alerts, contact messages and the B2B forms.
 *
 * One endpoint, allow-listed: the action has to be on the list below, only the
 * fields that action declares are forwarded, and anything marked `private` is
 * refused without a session. That last check matters because the middleware
 * does not cover `/api`, and several of these actions write against the
 * signed-in customer's own record.
 */

interface ActionDefinition {
  /** Path on the .NET API. */
  path: string;
  fields: readonly string[];
  /** Requires a signed-in customer. */
  private: boolean;
  /** Requests allowed per minute, per address. */
  limit: number;
}

const actions = {
  profile: {
    path: '/me',
    // `avatar` carries a URL this API issued from its own upload endpoint and
    // nothing else — the backend refuses any other value rather than storing
    // a link to somewhere it has never seen.
    fields: ['firstName', 'lastName', 'email', 'birthDate', 'city', 'nationalId', 'avatar'],
    private: true,
    limit: 10,
  },
  address: {
    path: '/me/addresses',
    fields: [
      'id', 'title', 'recipient', 'phone', 'province', 'city',
      'postalCode', 'line', 'isDefault',
    ],
    private: true,
    limit: 20,
  },
  'address-delete': {
    path: '/me/addresses/delete',
    fields: ['id'],
    private: true,
    limit: 20,
  },
  review: {
    path: '/reviews',
    fields: ['productSlug', 'rating', 'title', 'body', 'recommend'],
    private: true,
    limit: 5,
  },
  question: {
    path: '/questions',
    fields: ['productSlug', 'body'],
    private: true,
    limit: 5,
  },
  'return-request': {
    path: '/me/returns',
    fields: ['orderId', 'items', 'reason', 'description', 'refundMethod'],
    private: true,
    limit: 5,
  },
  /**
   * Cancelling one's own order.
   *
   * The order is the only field: the refund and any penalty are computed on the
   * API from what the order recorded and how far it got, so there is nothing
   * here a crafted body could inflate. The customer comes from the session, so
   * this can only ever reach the caller's own order.
   */
  'order-cancel': {
    path: '/me/orders/cancel',
    fields: ['orderId', 'reason'],
    private: true,
    limit: 5,
  },
  'notifications-read': {
    path: '/me/notifications/read',
    fields: ['ids'],
    private: true,
    limit: 30,
  },
  'wishlist-remove': {
    path: '/me/wishlist/remove',
    fields: ['productId'],
    private: true,
    limit: 30,
  },
  /**
   * Registering the browser for push notifications.
   *
   * All three fields come from the browser's own `PushSubscription` — the
   * endpoint it minted and the two keys that seal messages to it. Nothing here
   * is chosen by the page, and the API checks the shape of all three before
   * storing them. The customer is read from the session, so a subscription can
   * only ever be filed against the caller's own account.
   */
  'push-subscribe': {
    path: '/me/push/subscribe',
    fields: ['endpoint', 'p256dh', 'auth'],
    private: true,
    limit: 10,
  },
  'push-unsubscribe': {
    path: '/me/push/unsubscribe',
    fields: ['endpoint'],
    private: true,
    limit: 10,
  },
  'search-history-clear': {
    path: '/me/search-history/clear',
    fields: ['all'],
    private: true,
    limit: 10,
  },
  'wallet-topup': {
    path: '/me/wallet/topup',
    fields: ['amount'],
    private: true,
    // Moves money — a tighter ceiling than the read-only actions above.
    limit: 5,
  },
  // Settling a top-up the gateway has already taken payment for. Called once
  // per return from the gateway, and again by anyone who refreshes that page,
  // which the API treats as a no-op — so the ceiling is for abuse, not for
  // ordinary repeats.
  'wallet-topup-confirm': {
    path: '/me/wallet/topup/confirm',
    fields: ['reference'],
    private: true,
    limit: 20,
  },
  /*
    Settling whatever the shopper just came back from paying for.

    One action for orders and wallet top-ups both, because a gateway returns an
    authority and nothing that says which it was — the API resolves that against
    its own records. The gateway's `Status=OK` is deliberately not forwarded: it
    arrives in a query string in the shopper's own browser, so accepting it
    would be letting the buyer say whether they paid.
  */
  'payment-callback': {
    path: '/me/payments/callback',
    fields: ['reference'],
    private: true,
    limit: 20,
  },
  // A card-to-card transfer filed for review. The API refuses this outright
  // unless the store has enabled manual top-ups; forwarding it regardless keeps
  // that decision in one place rather than mirroring the flag here.
  'wallet-topup-manual': {
    path: '/me/wallet/topup/manual',
    fields: ['amount', 'trackingNumber', 'paidOn', 'receiptUrl', 'note'],
    private: true,
    limit: 5,
  },
  // Public: a visitor may ask to be told when something is back in stock, or
  // write to support, without an account.
  'stock-alert': {
    path: '/stock-alerts',
    fields: ['productSlug', 'phone', 'email'],
    private: false,
    limit: 5,
  },
  'contact-message': {
    path: '/support/messages',
    fields: ['name', 'phone', 'email', 'subject', 'body'],
    private: false,
    limit: 3,
  },
  'business-quote': {
    path: '/business/requests',
    fields: ['organization', 'contact', 'phone', 'email', 'items', 'description', 'deadline'],
    private: false,
    limit: 3,
  },
  'business-bulk': {
    path: '/business/bulk-orders',
    fields: ['organization', 'contact', 'phone', 'email', 'items', 'note'],
    private: false,
    limit: 3,
  },
  'business-organization': {
    path: '/business/organization',
    fields: [
      'organization', 'registrationNumber', 'economicCode', 'province',
      'city', 'address', 'phone', 'email',
    ],
    private: true,
    limit: 10,
  },
} as const satisfies Record<string, ActionDefinition>;

type ActionKey = keyof typeof actions;

function isActionKey(value: string): value is ActionKey {
  return Object.hasOwn(actions, value);
}

const MAX_FIELD_LENGTH = 2000;

/**
 * Ceiling on a list field — `items` on the two B2B forms and the return form,
 * `ids` on the notification sweep.
 *
 * The length check below only ever looked at strings, so an array field was
 * unbounded: a basket of a hundred thousand entries forwarded straight through
 * to the API, which then turned it into one query. Every screen that posts a
 * list here posts a page of one.
 */
const MAX_LIST_LENGTH = 200;

export async function POST(
  request: Request,
  { params }: { params: Promise<{ action: string }> },
) {
  const { action } = await params;
  if (!isActionKey(action)) {
    return NextResponse.json({ error: 'این درخواست شناخته نشد.' }, { status: 404 });
  }

  const definition: ActionDefinition = actions[action];
  const session = await getSession();

  if (definition.private && !session) {
    return NextResponse.json({ error: 'برای این کار وارد حساب خود شوید.' }, { status: 401 });
  }

  const key = session ? `account:${action}:${session.sub}` : clientKey(request, `account:${action}`);
  const limit = rateLimit(key, definition.limit, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as Record<string, unknown> | null;
  if (!body || typeof body !== 'object' || Array.isArray(body)) {
    return NextResponse.json({ error: 'داده ارسالی معتبر نیست.' }, { status: 400 });
  }

  const payload: Record<string, unknown> = {};
  for (const field of definition.fields) {
    if (!(field in body)) continue;
    const value = body[field];
    // A long free-text field is the cheapest way to abuse a write endpoint.
    if (typeof value === 'string' && value.length > MAX_FIELD_LENGTH) {
      return NextResponse.json({ error: 'مقدار وارد شده بیش از حد طولانی است.' }, { status: 400 });
    }
    if (Array.isArray(value) && value.length > MAX_LIST_LENGTH) {
      return NextResponse.json({ error: 'تعداد موارد ارسالی بیش از حد است.' }, { status: 400 });
    }
    payload[field] = value;
  }

  if (Object.keys(payload).length === 0) {
    return NextResponse.json({ error: 'هیچ اطلاعاتی ارسال نشد.' }, { status: 400 });
  }

  if (useMockData) {
    return NextResponse.json({ ok: true, action, saved: payload });
  }

  try {
    const saved = await api.post(definition.path, payload, {
      cache: 'no-store',
      ...(session
        ? {
            headers: {
              'X-Customer-Id': session.sub,
              // Proves the session predates no password reset. Without it the
              // API treats the request as unauthenticated.
              ...(session.stamp ? { 'X-Customer-Stamp': session.stamp } : null),
            },
          }
        : null),
    });

    // A write with nothing to say answers `204 No Content`, which `apiFetch`
    // turns into `undefined` — and `NextResponse.json(undefined)` throws rather
    // than producing an empty body. The forms only ever check `response.ok`, so
    // an acknowledgement is all this needs to be.
    return NextResponse.json(saved ?? { ok: true });
  } catch (cause) {
    // The reason the API gave, when it gave one — a cancellation the order's
    // status does not allow, or a stock alert for something already in stock,
    // both used to read as "the server is having a moment".
    const message = problemMessage(cause);

    return message
      ? NextResponse.json({ error: message }, { status: 400 })
      : NextResponse.json(
          { error: 'ثبت اطلاعات انجام نشد. کمی بعد دوباره تلاش کنید.' },
          { status: 502 },
        );
  }
}
