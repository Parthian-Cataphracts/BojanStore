/**
 * Browser-side writes for the panel.
 *
 * Admin forms post through here rather than calling `fetch` inline, so a failed
 * write always produces the same shape of error and the form can show the
 * server's own Persian message instead of a generic one.
 */

const GENERIC_ERROR = 'درخواست انجام نشد. دوباره تلاش کنید.';

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
    throw new SubmitError('ارتباط با سرور برقرار نشد. اتصال خود را بررسی کنید.', 0);
  }

  const payload = (await response.json().catch(() => null)) as
    | (Record<string, unknown> & { error?: unknown })
    | null;

  if (!response.ok) {
    throw new SubmitError(
      typeof payload?.error === 'string' ? payload.error : GENERIC_ERROR,
      response.status,
    );
  }

  return (payload ?? {}) as T;
}
