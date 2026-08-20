import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';
import { rateLimit } from '@/lib/auth/rate-limit';
import { useMockData } from '@/lib/api/mock-data';
import { problemMessage, type ProblemDetails } from '@/lib/api/problem';

/**
 * تایید ایمیل و شماره — the two verification toggles.
 *
 * Not on the `[resource]` allow-list proxy: that route only ever forwards a
 * `POST`, and the backend's `VerificationSettingsEndpoints.SaveSettings` is a
 * `PUT`. Owner-only, like the SMS and payment screens beside it.
 */
export async function PUT(request: Request) {
  const session = await getAdminSession();
  if (!session) {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  if (session.role !== 'owner') {
    return NextResponse.json(
      { error: 'برای این عملیات دسترسی لازم را ندارید.' },
      { status: 403 },
    );
  }

  const limit = rateLimit(`admin-write:${session.sub}`, 60, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as
    | { requireEmailVerification?: unknown; requirePhoneVerification?: unknown }
    | null;

  if (
    !body ||
    typeof body.requireEmailVerification !== 'boolean' ||
    typeof body.requirePhoneVerification !== 'boolean'
  ) {
    return NextResponse.json({ error: 'داده ارسالی معتبر نیست.' }, { status: 400 });
  }

  const payload = {
    requireEmailVerification: body.requireEmailVerification,
    requirePhoneVerification: body.requirePhoneVerification,
  };

  if (useMockData) {
    return NextResponse.json({ ok: true, ...payload });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  try {
    const upstream = await fetch(`${base.replace(/\/$/, '')}/settings/verification`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        'X-Admin-User': session.sub,
        'X-Admin-Stamp': session.stamp,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      body: JSON.stringify(payload),
      cache: 'no-store',
    });

    if (!upstream.ok) {
      const problem = (await upstream.json().catch(() => null)) as ProblemDetails | null;
      return NextResponse.json(
        { error: problemMessage(problem) ?? 'ذخیره تنظیمات انجام نشد.' },
        { status: upstream.status },
      );
    }

    return NextResponse.json({ ok: true, ...payload });
  } catch {
    return NextResponse.json(
      { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}
