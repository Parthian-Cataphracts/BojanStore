import { apiOrigins, securityHeaders } from '@bojan/config/security-headers';
import { distDir } from '@bojan/config/dist-dir';

const development = process.env.NODE_ENV !== 'production';

// Hosts the browser may load product media from. Kept in one place so
// `images.remotePatterns` and the content policy cannot fall out of step.
const imageHosts = ['https://lh3.googleusercontent.com'];

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,

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
