import { NextResponse } from 'next/server';
import { getSession } from '@/lib/auth/server';
import { rateLimit } from '@/lib/auth/rate-limit';
import { useMockData } from '@/lib/api/client';

/**
 * Customer file uploads — the profile picture, and return-request photos.
 *
 * The JSON endpoints cannot carry a file, so this is the one multipart route.
 * It forwards to the API's own upload endpoint rather than writing anything
 * itself: the API is where the type is sniffed from the file's magic bytes, the
 * size is capped and the stored name is generated, and duplicating any of that
 * here would mean two places to get it wrong.
 *
 * What this layer owns is the session. A signed-in customer, a folder from the
 * allow-list, and a bounded number of uploads per minute — then the request
 * travels on with the shared secret and the customer's id, which the API
 * honours only together.
 */

/**
 * Folders a *customer* may write to. Mirrors `CustomerFolders` in the API's
 * `UploadEndpoints`; `products` and the rest are operator-only and are not
 * reachable from this app at all.
 */
const FOLDERS = new Set(['avatars', 'returns', 'business']);

/** Matches the API's own ceiling, so a file that would be refused there is refused here first. */
const MAX_BYTES = 8 * 1024 * 1024;

export async function POST(
  request: Request,
  { params }: { params: Promise<{ folder: string }> },
) {
  const session = await getSession();
  if (!session) {
    return NextResponse.json({ error: 'برای این کار وارد حساب خود شوید.' }, { status: 401 });
  }

  const { folder } = await params;
  if (!FOLDERS.has(folder)) {
    return NextResponse.json({ error: 'این مقصد شناخته نشد.' }, { status: 404 });
  }

  const limit = rateLimit(`upload:${session.sub}`, 10, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  let file: File | null = null;
  try {
    const form = await request.formData();
    const value = form.get('file');
    if (value instanceof File) file = value;
  } catch {
    return NextResponse.json({ error: 'فایل ارسالی خوانده نشد.' }, { status: 400 });
  }

  if (!file || file.size === 0) {
    return NextResponse.json({ error: 'فایلی انتخاب نشده است.' }, { status: 400 });
  }

  if (file.size > MAX_BYTES) {
    return NextResponse.json({ error: 'حجم فایل باید کمتر از ۸ مگابایت باشد.' }, { status: 413 });
  }

  if (useMockData) {
    // Nothing to forward to. A data URL keeps the picker's preview working
    // locally without inventing a URL the API never issued — and the API
    // rejects exactly that kind of value, so this cannot be saved by mistake.
    const base64 = Buffer.from(await file.arrayBuffer()).toString('base64');
    return NextResponse.json({ url: `data:${file.type};base64,${base64}`, mock: true });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  // Rebuilt rather than streamed through: only the file field travels on, so
  // nothing else the browser put in the form reaches the API.
  const upstream = new FormData();
  upstream.append('file', file, file.name);

  try {
    const response = await fetch(`${base.replace(/\/$/, '')}/uploads/${folder}`, {
      method: 'POST',
      headers: {
        'X-Customer-Id': session.sub,
        ...(session.stamp ? { 'X-Customer-Stamp': session.stamp } : null),
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      body: upstream,
      cache: 'no-store',
    });

    if (!response.ok) {
      return NextResponse.json(
        { error: 'این فایل پذیرفته نشد. تصویری با فرمت JPG، PNG یا WebP انتخاب کنید.' },
        { status: response.status },
      );
    }

    return NextResponse.json((await response.json()) as unknown);
  } catch {
    return NextResponse.json(
      { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}
