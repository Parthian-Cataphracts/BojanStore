import { apiOrigins, securityHeaders } from '@bojan/config/security-headers';
import { distDir } from '@bojan/config/dist-dir';

const development = process.env.NODE_ENV !== 'production';

const imageHosts = ['https://lh3.googleusercontent.com'];

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,

  // Dev and production builds get separate directories so switching between
  // `pnpm dev:admin` and `pnpm build` never leaves one reading the other's output.
  distDir: distDir(),

  transpilePackages: ['@bojan/ui', '@bojan/config'],
  images: {
    remotePatterns: [{ protocol: 'https', hostname: 'lh3.googleusercontent.com' }],
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
    ];
  },
};

export default nextConfig;
