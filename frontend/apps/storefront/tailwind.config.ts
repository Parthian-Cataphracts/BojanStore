import type { Config } from 'tailwindcss';
import { preset } from '@bojan/config/tailwind-preset';

export default {
  presets: [preset],
  content: [
    './src/**/*.{ts,tsx,mdx}',
    // Scan the shared library so its classes survive purging.
    '../../packages/ui/src/**/*.{ts,tsx}',
  ],
} satisfies Config;
