import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';
import { rateLimit } from '@/lib/auth/rate-limit';
import {
  isResourceKey,
  pickAllowedFields,
  resources,
  type ResourceDefinition,
} from '@/lib/api/resources';
import { useMockData } from '@/lib/api/mock-data';
import { problemMessage, type ProblemDetails } from '@/lib/api/problem';

/**
 * Writes from the panel's forms.
 *
 * One endpoint, but not a generic one: the resource has to be on the allow-list
 * in `lib/api/resources`, the operator's role has to include it, and only the
 * fields that resource declares are forwarded. A form cannot reach a resource
 * it was not written for, and a crafted body cannot set a field its form does
 * not show.
 *
 * While the fixtures are in use there is nothing to forward to, so the request
 * is validated and acknowledged. Every check above still runs — the mock path
 * is the same code with the network call skipped, not a bypass.
 *
 * That flag comes from `lib/api/client` rather than being read again here. It
 * used to be its own copy of `!== 'false'`, which meant a production deploy
 * that had not set the variable acknowledged every write and forwarded none of
 * them: the operator saved a product, saw it succeed, and nothing had changed.
 */

const MAX_BODY_KEYS = 60;

export async function POST(
  request: Request,
  { params }: { params: Promise<{ resource: string }> },
) {
  const session = await getAdminSession();
  if (!session) {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  const { resource } = await params;
  if (!isResourceKey(resource)) {
    return NextResponse.json({ error: 'این منبع وجود ندارد.' }, { status: 404 });
  }

  // Widened from the `as const` literal so the role list is comparable against
  // any `AdminRole` rather than only the ones this entry happens to name.
  const definition: ResourceDefinition = resources[resource];

  if (!definition.roles.includes(session.role)) {
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

  const body = (await request.json().catch(() => null)) as Record<string, unknown> | null;
  if (!body || typeof body !== 'object' || Array.isArray(body)) {
    return NextResponse.json({ error: 'داده ارسالی معتبر نیست.' }, { status: 400 });
  }
  if (Object.keys(body).length > MAX_BODY_KEYS) {
    return NextResponse.json({ error: 'داده ارسالی بیش از حد بزرگ است.' }, { status: 413 });
  }

  const payload = pickAllowedFields(definition, body);
  if (Object.keys(payload).length === 0) {
    return NextResponse.json({ error: 'هیچ فیلد قابل ذخیره‌ای ارسال نشد.' }, { status: 400 });
  }

  if (useMockData) {
    // Creating a key means minting real secret material — there is nothing
    // for mock mode to fabricate that would not repeat the bug where a fake
    // secret got shown in place of the one the backend actually generated.
    // Every other resource is fine echoing back what would be saved.
    if (resource === 'api-keys' && !('revoked' in payload)) {
      return NextResponse.json(
        { error: 'ساخت کلید API نیازمند اتصال به سرور واقعی است.' },
        { status: 501 },
      );
    }
    return NextResponse.json({ ok: true, resource, saved: payload });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  try {
    const upstream = await fetch(`${base.replace(/\/$/, '')}${definition.path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        // The operator's identity travels as a header the backend honours only
        // alongside the shared secret below — the browser never sees or sets
        // either. The backend reads the *role* from its own records rather than
        // from anything sent here.
        'X-Admin-User': session.sub,
        'X-Admin-Stamp': session.stamp,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      body: JSON.stringify(payload),
      cache: 'no-store',
    });

    if (!upstream.ok) {
      // The API answers RFC 7807 — a machine key in `title`, sometimes a field
      // name in `detail`. This read `error`, which it has never sent, so every
      // refusal fell through to "ذخیره اطلاعات انجام نشد": a permission the
      // owner has not granted, a slug already taken and a real server fault all
      // looked the same, and none of them said what to do about it.
      const problem = (await upstream.json().catch(() => null)) as ProblemDetails | null;
      return NextResponse.json(
        { error: problemMessage(problem) ?? 'ذخیره اطلاعات انجام نشد.' },
        { status: upstream.status },
      );
    }

    return NextResponse.json((await upstream.json().catch(() => ({ ok: true }))) as unknown);
  } catch {
    return NextResponse.json(
      { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}
