import localFont from 'next/font/local';

/**
 * Self-hosted, not `next/font/google`.
 *
 * The Google Fonts fetch that `next/font/google` performs at build/dev-server
 * start needs a working connection to fonts.googleapis.com; on a slow or
 * unreliable link it times out, both apps fall back to the system font, and
 * every recompile pays for the retry — which is exactly what made the site
 * feel slow and look unstyled at the same time. These are the same two
 * variable-font files Google was serving (Vazirmatn's Arabic-script subset
 * plus both families' Latin subset, all four Latin weights folded into one
 * variable file each), just checked in instead of fetched.
 */
export const vazirmatn = localFont({
  src: [
    { path: './vazirmatn-arabic.woff2', style: 'normal' },
    { path: './vazirmatn-latin.woff2', style: 'normal' },
  ],
  variable: '--font-vazirmatn',
  display: 'swap',
  weight: '100 900',
});

/** Latin technical values only — SKUs, API keys, URLs, e-mail addresses. */
export const inter = localFont({
  src: [{ path: './inter-latin.woff2', style: 'normal' }],
  variable: '--font-inter',
  display: 'swap',
  weight: '100 900',
});
