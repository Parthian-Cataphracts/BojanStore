import { fileURLToPath } from 'node:url';
import { apiOrigins, securityHeaders } from '@bojan/config/security-headers';
import { distDir } from '@bojan/config/dist-dir';

const development = process.env.NODE_ENV !== 'production';

// Hosts the browser may load product media from. Kept in one place so
// `images.remotePatterns` and the content policy cannot fall out of step.
const imageHosts = ['https://lh3.googleusercontent.com'];

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,

  /*
   * Emits `.next/standalone` — a server plus only the files the build traced
   * as reachable. The container image copies that instead of the workspace's
   * node_modules, which for a pnpm monorepo is the difference between shipping
   * a few megabytes and shipping every dependency of every app.
   *
   * Opt-in rather than always on, because producing it means reproducing pnpm's
   * symlink farm, and creating a symlink on Windows needs a privilege a normal
   * account does not have — the copy dies with EPERM at the end of an otherwise
   * finished build. The Dockerfile sets this; `pnpm build` on a workstation
   * does not, and is unaffected.
   */
  ...(process.env.BUILD_STANDALONE === '1'
    ? {
        output: 'standalone',
        // The workspace root, not the app directory: from `apps/storefront`
        // the trace misses the symlinked `@bojan/*` packages and the pnpm store
        // hoisted above it, and the standalone server starts with modules
        // missing. `fileURLToPath`, not `URL.pathname` — the latter yields
        // "/C:/Users/..." on Windows, which no path API here accepts.
        outputFileTracingRoot: fileURLToPath(new URL('../..', import.meta.url)),
      }
    : null),

  // Dev and production builds get separate directories so switching between
  // `pnpm dev` and `pnpm build` never leaves one reading the other's output.
  distDir: distDir(),

  // The UI package ships raw TSX; Next compiles it as part of the app build.
  transpilePackages: ['@bojan/ui', '@bojan/config'],

  images: {
    remotePatterns: [
      // Design mock imagery. Replace with the store's own CDN host once the
      // .NET backend starts serving product media.
      { protocol: 'https', hostname: 'lh3.googleusercontent.com' },
    ],
    // AVIF first, WebP for the browsers without it. The catalogue is almost
    // entirely photographs, where AVIF is roughly half the bytes of the JPEG
    // the remote host serves.
    formats: ['image/avif', 'image/webp'],
    // Remote media is immutable in practice — the URL changes when the picture
    // does — so the default 60-second optimiser cache just means re-encoding
    // the same file all day.
    minimumCacheTTL: 60 * 60 * 24 * 30,
  },

  eslint: {
    ignoreDuringBuilds: false,
  },

  // Declared here rather than in the middleware so they also cover statically
  // generated pages, which the middleware matcher deliberately skips.
  async headers() {
    return [
      {
        source: '/:path*',
        headers: securityHeaders({
          development,
          imgSrc: imageHosts,
          connectSrc: apiOrigins(process.env.NEXT_PUBLIC_API_BASE_URL, process.env.API_BASE_URL),
        }),
      },
    ];
  },
};

export default nextConfig;
