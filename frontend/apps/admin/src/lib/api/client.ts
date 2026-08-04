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
}

async function adminHeaders(): Promise<Record<string, string>> {
  if (typeof window !== 'undefined') return {};
  try {
    const { getAdminSession } = await import('@/lib/auth/server');
    const session = await getAdminSession();
    if (!session) return {};
    return { 'X-Admin-User': session.sub };
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
      ...(typeof window === 'undefined' && API_KEY ? { 'X-Api-Key': API_KEY } : null),
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
