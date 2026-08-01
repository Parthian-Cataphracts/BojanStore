/**
 * Response security headers, shared by both applications.
 *
 * Plain ESM rather than TypeScript because `next.config.mjs` is loaded by Node
 * directly and cannot import a `.ts` module.
 *
 * Applied through `next.config.mjs` rather than the middleware so they cover
 * every response — including statically generated pages, which the middleware
 * matcher deliberately skips.
 *
 * The content policy is source-restrictive rather than nonce-based. A nonce has
 * to be minted per request, which would force the whole catalogue out of static
 * generation; blocking every off-origin script source still removes the
 * delivery mechanism most injected payloads rely on. `'unsafe-inline'` for
 * scripts is what Next's hydration bootstrap requires, and is the one
 * concession here.
 */

/**
 * @typedef {object} SecurityHeaderOptions
 * @property {string[]} [connectSrc] Extra origins the app talks to — the .NET API, a media CDN.
 * @property {string[]} [imgSrc] Extra image hosts, matching `images.remotePatterns`.
 * @property {boolean} [development] Relaxes the policy for `next dev`, which needs eval for fast refresh.
 */

/**
 * @param {SecurityHeaderOptions} [options]
 * @returns {{ key: string, value: string }[]}
 */
export function securityHeaders(options = {}) {
  const { connectSrc = [], imgSrc = [], development = false } = options;

  const policy = [
    "default-src 'self'",
    `script-src 'self' 'unsafe-inline'${development ? " 'unsafe-eval'" : ''}`,
    // Tailwind ships no inline styles, but `next/font` injects one. Fonts are
    // self-hosted from /public/fonts (see packages/ui/src/fonts) — no
    // fonts.googleapis.com/fonts.gstatic.com allowance needed.
    "style-src 'self' 'unsafe-inline'",
    "font-src 'self' data:",
    ["img-src 'self' data: blob:", ...imgSrc].join(' '),
    // `next dev` opens a websocket back to the dev server for fast refresh.
    ["connect-src 'self'", ...connectSrc, ...(development ? ['ws:'] : [])].join(' '),
    // Neither app embeds a plugin, an iframe, or expects to be framed.
    "object-src 'none'",
    "frame-src 'none'",
    "frame-ancestors 'none'",
    // Stops an injected <base> rewriting every relative URL on the page.
    "base-uri 'self'",
    // Forms may only post back to this origin.
    "form-action 'self'",
    "worker-src 'self' blob:",
    ...(development ? [] : ['upgrade-insecure-requests']),
  ].join('; ');

  return [
    { key: 'Content-Security-Policy', value: policy },
    // Redundant with `frame-ancestors` on modern browsers; cheap to keep.
    { key: 'X-Frame-Options', value: 'DENY' },
    { key: 'X-Content-Type-Options', value: 'nosniff' },
    { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
    {
      key: 'Permissions-Policy',
      value: 'camera=(), microphone=(), geolocation=(), payment=(), usb=()',
    },
    ...(development
      ? []
      : [
          {
            key: 'Strict-Transport-Security',
            value: 'max-age=63072000; includeSubDomains; preload',
          },
        ]),
  ];
}

/**
 * Origins a `connect-src` needs, derived from the configured API base URLs.
 * Returns the origin only — a path in `connect-src` is ignored by browsers.
 *
 * @param {(string | undefined)[]} urls
 * @returns {string[]}
 */
export function apiOrigins(...urls) {
  const origins = new Set();

  for (const url of urls) {
    if (!url) continue;
    try {
      origins.add(new URL(url).origin);
    } catch {
      // Not a absolute URL — a same-origin path needs no allowance.
    }
  }

  return [...origins];
}
