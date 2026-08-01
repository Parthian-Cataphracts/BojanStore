import { NextResponse } from 'next/server';
import { getSession } from '@/lib/auth/server';
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
    fields: ['firstName', 'lastName', 'email', 'birthDate', 'city', 'nationalId'],
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
  'search-history-clear': {
    path: '/me/search-history/clear',
    fields: ['all'],
    private: true,
    limit: 10,
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
      ...(session ? { headers: { 'X-Customer-Id': session.sub } } : null),
    });

    // A write with nothing to say answers `204 No Content`, which `apiFetch`
    // turns into `undefined` — and `NextResponse.json(undefined)` throws rather
    // than producing an empty body. The forms only ever check `response.ok`, so
    // an acknowledgement is all this needs to be.
    return NextResponse.json(saved ?? { ok: true });
  } catch {
    return NextResponse.json(
      { error: 'ثبت اطلاعات انجام نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}
