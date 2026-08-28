import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';
import { rateLimit } from '@/lib/auth/rate-limit';
import { useMockData } from '@/lib/api/mock-data';
import { problemMessage, type ProblemDetails } from '@/lib/api/problem';

/**
 * اتصال به نایت — connecting this shop to the platform that delivers its
 * Features, and disconnecting it again.
 *
 * Not on the `[resource]` allow-list proxy: that route forwards a `POST` to a
 * named resource, and these are two verbs against two paths. Owner-only, and
 * this is the clearest case for it in the panel — the credential entered here
 * lets a control plane install code and configuration into this shop.
 *
 * The client secret passes through this server and is never stored by it. It
 * goes to the backend, which keeps it; nothing here logs it, and no read path
 * anywhere returns it.
 */
async function forward(
  path: 'connect' | 'disconnect',
  payload: unknown,
  session: { sub: string; stamp: string },
) {
  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;

  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  try {
    const upstream = await fetch(`${base.replace(/\/$/, '')}/knight/${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        'X-Admin-User': session.sub,
        'X-Admin-Stamp': session.stamp,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      body: JSON.stringify(payload ?? {}),
      cache: 'no-store',
    });

    const body = (await upstream.json().catch(() => null)) as ProblemDetails | null;

    if (!upstream.ok) {
      return NextResponse.json(
        {
          error:
            problemMessage(body) ??
            (path === 'connect' ? 'اتصال به نایت انجام نشد.' : 'قطع اتصال انجام نشد.'),
        },
        { status: upstream.status },
      );
    }

    // The status as the backend now sees it — «ثبت شد و هنوز دست ندادیم».
    // The handshake happens on the agent's next pass, and the screen shows it
    // when it does rather than claiming it here.
    return NextResponse.json(body ?? { ok: true });
  } catch {
    return NextResponse.json(
      { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}

async function guard() {
  const session = await getAdminSession();

  if (!session) {
    return { error: NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 }) };
  }

  if (session.role !== 'owner') {
    return {
      error: NextResponse.json(
        { error: 'برای این عملیات دسترسی لازم را ندارید.' },
        { status: 403 },
      ),
    };
  }

  const limit = rateLimit(`admin-write:${session.sub}`, 60, 60);

  if (!limit.allowed) {
    return {
      error: NextResponse.json(
        { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
        { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
      ),
    };
  }

  return { session };
}

export async function POST(request: Request) {
  const { error, session } = await guard();
  if (error || !session) return error;

  const body = (await request.json().catch(() => null)) as
    | { baseUrl?: unknown; clientId?: unknown; clientSecret?: unknown; environment?: unknown }
    | null;

  if (
    !body ||
    typeof body.baseUrl !== 'string' ||
    typeof body.clientId !== 'string' ||
    typeof body.clientSecret !== 'string'
  ) {
    return NextResponse.json({ error: 'داده ارسالی معتبر نیست.' }, { status: 400 });
  }

  if (useMockData) {
    // Refused rather than faked. This screen is about whether a real connection
    // exists, and a fixture that reported success would be the one lie the
    // screen cannot afford.
    return NextResponse.json(
      { error: 'در حالت نمایشی نمی‌توان به نایت وصل شد.' },
      { status: 503 },
    );
  }

  return forward(
    'connect',
    {
      baseUrl: body.baseUrl.trim(),
      clientId: body.clientId.trim(),
      clientSecret: body.clientSecret.trim(),
      environment: typeof body.environment === 'string' ? body.environment.trim() : undefined,
    },
    session,
  );
}

export async function DELETE() {
  const { error, session } = await guard();
  if (error || !session) return error;

  if (useMockData) {
    return NextResponse.json({ error: 'در حالت نمایشی اتصالی وجود ندارد.' }, { status: 503 });
  }

  return forward('disconnect', {}, session);
}
