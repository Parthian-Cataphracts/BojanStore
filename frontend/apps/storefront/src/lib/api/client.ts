/**
 * Thin typed fetch wrapper around the .NET 10 backend.
 *
 * Server components hit `API_BASE_URL` directly; anything running in the
 * browser uses the public variable. Errors surface as `ApiError` so callers can
 * distinguish a 404 from a transport failure.
 */

const SERVER_BASE = process.env.API_BASE_URL;
const CLIENT_BASE = process.env.NEXT_PUBLIC_API_BASE_URL;

export const useMockData = process.env.NEXT_PUBLIC_USE_MOCK_DATA !== 'false';

/**
 * Shared secret proving to the API that a request came from this server.
 *
 * Deliberately *not* `NEXT_PUBLIC_` — it must never reach a browser bundle. It
 * is only ever read on the server, and the API treats the customer id below as
 * an assertion rather than a claim precisely because this accompanies it.
 */
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
  /** Query string parameters; `undefined` values are dropped. */
  query?: Record<string, string | number | boolean | undefined>;
  body?: unknown;
  /** Next.js caching hints, forwarded verbatim. */
  next?: { revalidate?: number | false; tags?: string[] };
  /**
   * Send the signed-in customer's credential.
   *
   * Off by default: the catalogue is public and cacheable, and reading the
   * session there would opt those pages out of static generation for a header
   * the API ignores. Every per-user resource sets it — see the `noStore`
   * constant each of the account modules shares.
   */
  auth?: boolean;
}

/**
 * The signed-in customer's credential, read from the session cookie.
 *
 * `next/headers` is imported dynamically so it never lands in a client bundle,
 * and a failure is swallowed rather than thrown: outside a request scope there
 * is simply no session, and that is a 401 from the API rather than a crash here.
 */
async function customerHeaders(): Promise<Record<string, string>> {
  if (typeof window !== 'undefined') return {};

  try {
    const { getSession } = await import('@/lib/auth/server');
    const session = await getSession();
    if (!session) return {};

    return {
      // Both schemes the API accepts. The bearer token is the one that ages
      // well; the id is what the API falls back to for a session minted before
      // the backend started issuing tokens.
      ...(session.token ? { Authorization: `Bearer ${session.token}` } : null),
      'X-Customer-Id': session.sub,
    };
  } catch {
    return {};
  }
}

export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { query, body, headers, next, auth, ...init } = options;

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
      // Sent on every server-side call, public reads included: it identifies
      // this server to the API, which is a different question from who the
      // shopper is.
      ...(typeof window === 'undefined' && API_KEY ? { 'X-Api-Key': API_KEY } : null),
      ...(auth ? await customerHeaders() : null),
      // Caller-supplied headers win — the write proxy already sets its own
      // X-Customer-Id from the session it just verified.
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

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
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
