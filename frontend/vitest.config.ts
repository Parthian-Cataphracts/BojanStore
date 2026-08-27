import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

const here = (path: string) => fileURLToPath(new URL(path, import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // Mirror the aliases the apps use so tests import the same modules the
      // build does, rather than a parallel copy.
      '@bojan/ui': here('./packages/ui/src/index.ts'),
      // Subpaths before the bare name: these aliases match by prefix, so the
      // bare entry would otherwise swallow `@bojan/config/safe-next` and
      // resolve it to the package index.
      '@bojan/config/safe-next': here('./packages/config/src/safe-next.ts'),
      '@bojan/config/origin': here('./packages/config/src/origin.ts'),
      '@bojan/config/client-address': here('./packages/config/src/client-address.ts'),
      '@bojan/config/submit-errors': here('./packages/config/src/submit-errors.js'),
      '@bojan/config': here('./packages/config/src/index.ts'),
      '@/': `${here('./apps/storefront/src')}/`,
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: [here('./vitest.setup.ts')],
    include: [
      'packages/*/src/**/*.test.{ts,tsx}',
      'apps/*/src/**/*.test.{ts,tsx}',
      'apps/*/tests/**/*.test.{ts,tsx}',
    ],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      // Only logic is worth a coverage number; pages are covered by the
      // route sweep and the production build, not by unit tests.
      include: ['packages/ui/src/lib/**', 'packages/config/src/**', 'apps/*/src/lib/**'],
      exclude: ['**/mock/**', '**/*.test.*'],
    },
  },
});
