/**
 * Browser-side writes for the panel.
 *
 * Admin forms post through here rather than calling `fetch` inline, so a failed
 * write always produces the same shape of error and the form can show the
 * server's own Persian message instead of a generic one.
 *
 * When the server sends nothing readable — a proxy timing out, a stale tab
 * asking for a route the new build does not serve — the status decides what the
 * operator is told, and every one of those sentences says what to do next. See
 * `@bojan/config/submit-errors`.
 */

import { retryAfterSeconds, submitErrorMessage } from '@bojan/config/submit-errors';

export class SubmitError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = 'SubmitError';
  }
}

export async function postJson<T = unknown>(
  path: string,
  body?: unknown,
  options: { method?: 'POST' | 'PUT' | 'PATCH' | 'DELETE' } = {},
): Promise<T> {
  let response: Response;

  try {
    response = await fetch(path, {
      method: options.method ?? 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      credentials: 'same-origin',
      ...(body !== undefined ? { body: JSON.stringify(body) } : null),
    });
  } catch {
    throw new SubmitError(submitErrorMessage(0), 0);
  }

  const payload = (await response.json().catch(() => null)) as
    | (Record<string, unknown> & { error?: unknown })
    | null;

  if (!response.ok) {
    // The server's own sentence first: it knows which field was wrong, and
    // these do not.
    throw new SubmitError(
      typeof payload?.error === 'string' && payload.error.length > 0
        ? payload.error
        : submitErrorMessage(response.status, {
            retryAfterSeconds: retryAfterSeconds(response.headers),
          }),
      response.status,
    );
  }

  return (payload ?? {}) as T;
}
