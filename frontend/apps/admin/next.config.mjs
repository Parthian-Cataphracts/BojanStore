import { fileURLToPath } from 'node:url';
import { apiOrigins, fontCacheHeaders, securityHeaders } from '@bojan/config/security-headers';
import { distDir } from '@bojan/config/dist-dir';

const development = process.env.NODE_ENV !== 'production';

// See the storefront's config: the API serves uploaded media, so the panel's
// product-image picker loads from there. Kept in one list so the content policy
// and `images.remotePatterns` cannot disagree.
const imageHosts = [
  'https://lh3.googleusercontent.com',
  ...apiOrigins(process.env.NEXT_PUBLIC_API_BASE_URL),
];

const imagePatterns = imageHosts.map((value) => {
  const origin = new URL(value);
  return {
    protocol: origin.protocol.replace(':', ''),
    hostname: origin.hostname,
    ...(origin.port ? { port: origin.port } : null),
  };
});

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,

  // See the storefront config: ship the traced standalone server rather than
  // the monorepo's node_modules, traced from the workspace root so the
  // symlinked `@bojan/*` packages come with it. Set by the Dockerfile only —
  // reproducing pnpm's symlinks fails with EPERM on Windows.
  ...(process.env.BUILD_STANDALONE === '1'
    ? {
        output: 'standalone',
        outputFileTracingRoot: fileURLToPath(new URL('../..', import.meta.url)),
      }
    : null),

  // Dev and production builds get separate directories so switching between
  // `pnpm dev:admin` and `pnpm build` never leaves one reading the other's output.
  distDir: distDir(),

  transpilePackages: ['@bojan/ui', '@bojan/config'],
  images: {
    remotePatterns: imagePatterns,
  },

  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          ...securityHeaders({
            development,
            imgSrc: imageHosts,
            connectSrc: apiOrigins(process.env.NEXT_PUBLIC_API_BASE_URL, process.env.API_BASE_URL),
          }),
          // Belt and braces alongside the `robots` metadata in the layout: the
          // panel must never appear in an index, however it is reached.
          { key: 'X-Robots-Tag', value: 'noindex, nofollow, noarchive' },
        ],
      },
      ...fontCacheHeaders(),
    ];
  },
};

export default nextConfig;
