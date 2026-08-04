/**
 * Browser-side writes.
 *
 * Form components post through here rather than calling `fetch` inline, so
 * error handling is uniform: a non-2xx response becomes a thrown `Error`
 * carrying the server's Persian message, which the form renders in the field
 * or banner it already has.
 *
 * These hit this app's own route handlers (`/api/...`), not the .NET backend
 * directly — the handler is what holds the session cookie and the rate limit.
 */

const GENERIC_ERROR = 'درخواست انجام نشد. دوباره تلاش کنید.';

/**
 * A form's named fields as a plain object.
 *
 * File inputs are skipped: uploads need multipart, which the JSON endpoints do
 * not take. Repeated names collapse to the last value, which is what every form
 * in this app expects — none of them uses a repeated name.
 */
export function formPayload(form: HTMLFormElement): Record<string, string> {
  const payload: Record<string, string> = {};
  for (const [key, value] of new FormData(form).entries()) {
    if (typeof value === 'string') payload[key] = value;
  }
  return payload;
}

export class SubmitError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = 'SubmitError';
  }
}

/**
 * A key identifying one attempt at a write that must not happen twice.
 *
 * Generated per attempt and reused across retries of it, so a double-tap or a
 * reconnect collapses into one result while a deliberate second attempt is a
 * second request. `crypto.randomUUID` needs a secure context, which every page
 * that places an order is; the fallback keeps a plain-HTTP development host
 * working rather than throwing at the moment of purchase.
 */
export function newIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `k-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

export async function postJson<T = unknown>(
  path: string,
  body?: unknown,
  options: {
    method?: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
    signal?: AbortSignal;
    /** Extra request headers — the order routes send `Idempotency-Key`. */
    headers?: Record<string, string>;
  } = {},
): Promise<T> {
  let response: Response;

  try {
    response = await fetch(path, {
      method: options.method ?? 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...options.headers },
      // The session cookie is http-only; `same-origin` is what sends it.
      credentials: 'same-origin',
      ...(body !== undefined ? { body: JSON.stringify(body) } : null),
      ...(options.signal ? { signal: options.signal } : null),
    });
  } catch {
    throw new SubmitError('ارتباط با سرور برقرار نشد. اتصال خود را بررسی کنید.', 0);
  }

  const payload = (await response.json().catch(() => null)) as
    | (Record<string, unknown> & { error?: unknown })
    | null;

  if (!response.ok) {
    const message = typeof payload?.error === 'string' ? payload.error : GENERIC_ERROR;
    throw new SubmitError(message, response.status);
  }

  return (payload ?? {}) as T;
}
