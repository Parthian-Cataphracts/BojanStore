import { getAdminSession } from '@/lib/auth/server';

/**
 * Same-origin, staff-authenticated gateway to a delivered Feature's screen.
 *
 * A delivered Feature's UI is served by the store API under `/api/features/...`
 * behind the KNIGHT proxy, which decides who the caller is from the identity the
 * store asserts — never from a cookie it is handed. So the browser cannot reach
 * that proxy directly: this handler is the one thing that can, forwarding the
 * request to the API with the same trusted-proxy headers every other admin call
 * uses (`X-Api-Key` + `X-Admin-User`/`X-Admin-Stamp`), which is what makes the
 * proxy assert `staff`. An operator's iframe then loads `/api/features/<prefix>`
 * on this origin and sees the screen; a shopper, with no admin session, gets a
 * 401 here and never reaches the API at all.
 *
 * Read-only: only GET is forwarded, because the panel mounts these screens in an
 * iframe and nothing in this release posts through them. A Feature that needs
 * writes gets them added here deliberately, not by default.
 */
export async function GET(
  request: Request,
  { params }: { params: Promise<{ path: string[] }> },
) {
  const session = await getAdminSession();
  if (!session) {
    return new Response('unauthorized', { status: 401 });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return new Response('API address is not configured.', { status: 500 });
  }

  const { path } = await params;
  const suffix = (path ?? []).map(encodeURIComponent).join('/');
  const query = new URL(request.url).search;
  const target = `${base.replace(/\/$/, '')}/api/features/${suffix}${query}`;

  let upstream: Response;
  try {
    upstream = await fetch(target, {
      method: 'GET',
      headers: {
        Accept: request.headers.get('accept') ?? '*/*',
        'X-Admin-User': session.sub,
        'X-Admin-Stamp': session.stamp,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      cache: 'no-store',
    });
  } catch {
    return new Response('The feature service could not be reached.', { status: 502 });
  }

  // Pass the body and content type straight through — the screen is the
  // Feature's own HTML, and reshaping it here would be this panel deciding what
  // another team's screen looks like.
  const body = await upstream.arrayBuffer();
  const headers = new Headers();
  const contentType = upstream.headers.get('content-type');
  if (contentType) headers.set('content-type', contentType);
  headers.set('cache-control', 'no-store');
  return new Response(body, { status: upstream.status, headers });
}
