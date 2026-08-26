/**
 * Thin typed fetch wrapper around the .NET admin API.
 *
 * Mirrors the storefront's `lib/api/client.ts`, but the panel has no bearer
 * token — `AdminSession` carries only `sub`/`role` — so the operator's
 * identity travels as `X-Admin-User`, the same header the write proxy already
 * sends. Server components hit `API_BASE_URL` directly; the browser uses the
 * public variable. Errors surface as `ApiError` so callers can distinguish a
 * 404 from a transport failure.
 */

import { clientAddress } from '@bojan/config/client-address';

const SERVER_BASE = process.env.API_BASE_URL;
const CLIENT_BASE = process.env.NEXT_PUBLIC_API_BASE_URL;

// Re-exported so the many server-side callers that already read it from here
// keep working. Anything that runs in the browser must import it from
// `./mock-data` directly — see the note there.
export { useMockData } from './mock-data';

const API_KEY = process.env.API_KEY;

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly body?: unknown,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

function baseUrl(): string {
  const base = typeof window === 'undefined' ? (SERVER_BASE ?? CLIENT_BASE) : CLIENT_BASE;
  if (!base) {
    throw new Error(
      'API base URL is not configured. Set API_BASE_URL / NEXT_PUBLIC_API_BASE_URL (see .env.example).',
    );
  }
  return base.replace(/\/$/, '');
}

export interface RequestOptions extends Omit<RequestInit, 'body'> {
  query?: Record<string, string | number | boolean | undefined>;
  body?: unknown;
  next?: { revalidate?: number | false; tags?: string[] };
  auth?: boolean;
  /**
   * Forward the operator's own address to the API. Defaults to on for
   * everything but GET — see `forwardedForHeader`.
   */
  forwardClient?: boolean;
}

/**
 * The operator's own address, forwarded to the API.
 *
 * Mirrors the storefront client's header of the same name, and exists for the
 * same reason: every call this server makes reaches the API from one address,
 * so the API's per-address limits counted the whole panel as a single client.
 * Sixty writes a minute and eight sign-in attempts per five minutes were shared
 * by every operator at once — a shop with four people packing orders spent the
 * write budget between them, and one operator mistyping a password four times
 * used half the sign-in budget for the building.
 *
 * `X-Forwarded-For` is the header the API already reads through
 * `UseForwardedHeaders`, and this container is inside the private range it
 * trusts, so one entry here becomes the caller's address there. The value is
 * the derived client address rather than the incoming chain — see
 * `@bojan/config/client-address` for which end of that chain is the caller's
 * and which end is the caller's own invention.
 *
 * Skipped for GET so a statically rendered page is not opted out of static
 * rendering by reading `headers()`; the panel's reads are covered by the API's
 * global ceiling, which exempts this server by name.
 */
async function forwardedForHeader(
  method: string,
  forwardClient: boolean | undefined,
): Promise<Record<string, string>> {
  if (typeof window !== 'undefined') return {};
  if (!(forwardClient ?? method.toUpperCase() !== 'GET')) return {};

  try {
    const { headers } = await import('next/headers');
    const address = clientAddress(await headers());
    return address ? { 'X-Forwarded-For': address } : {};
  } catch {
    // No request scope — a build-time render, or a background job.
    return {};
  }
}

async function adminHeaders(): Promise<Record<string, string>> {
  if (typeof window !== 'undefined') return {};
  try {
    const { getAdminSession } = await import('@/lib/auth/server');
    const session = await getAdminSession();
    if (!session) return {};
    // The stamp travels with the id, never without it: the API refuses an
    // operator header that arrives unstamped, which is what makes a session
    // revocable at all.
    return { 'X-Admin-User': session.sub, 'X-Admin-Stamp': session.stamp };
  } catch {
    return {};
  }
}

export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { query, body, headers, next, auth, forwardClient, ...init } = options;

  const url = new URL(`${baseUrl()}${path.startsWith('/') ? path : `/${path}`}`);
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined) url.searchParams.set(key, String(value));
  }

  const response = await fetch(url, {
    ...init,
    ...(next ? { next } : null),
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : null),
      ...(typeof window === 'undefined' && API_KEY ? { 'X-Api-Key': API_KEY } : null),
      ...(await forwardedForHeader(init.method ?? 'GET', forwardClient)),
      ...(auth ? await adminHeaders() : null),
      ...headers,
    },
    ...(body !== undefined ? { body: JSON.stringify(body) } : null),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => undefined);
    throw new ApiError(
      `درخواست ${path} با خطای ${response.status} مواجه شد.`,
      response.status,
      payload,
    );
  }

  // 204 is not the only answer with nothing in it. A write that succeeds and
  // has nothing to say comes back 200 with an empty body — `Results.Ok()` on
  // the API side — and `response.json()` throws on that, which reached the
  // caller as a failed request for something that had in fact worked. Sending
  // a chat message was the visible one: the message was stored and the widget
  // said it could not be sent.
  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  const text = await response.text();
  return (text.length > 0 ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) => apiFetch<T>(path, { ...options, method: 'GET' }),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'PUT', body }),
  patch: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'PATCH', body }),
  delete: <T>(path: string, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'DELETE' }),
};
