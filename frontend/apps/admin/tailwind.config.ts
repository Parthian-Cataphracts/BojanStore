import type { Config } from 'tailwindcss';
import { preset } from '@bojan/config/tailwind-preset';

/**
 * Fluid heading sizes, overriding the preset's fixed ones for this app only.
 *
 * The scale was a pair of fixed sizes with a step at `md`: a title was 20px up
 * to 767px and 29px from 768px, and nothing in between. On the two widths
 * either side of that line the same screen looked like two different designs,
 * and every heading in the panel carried the step in its class list —
 * `text-card-title md:text-section-title`, nine times over — so the jump was
 * something each page had to remember rather than something the scale did.
 *
 * `clamp` replaces both halves. The middle term is a line through two points:
 * the minimum at a 360px viewport and the maximum at 1536px, expressed as a
 * rem intercept plus a vw slope so it tracks the window continuously and still
 * respects the reader's own font size. Outside that range the clamp holds it,
 * so a phone never goes below the minimum and a 4K monitor never runs away
 * with it.
 *
 * Headings only. Body copy, labels and table cells stay fixed: this is an
 * operations panel read at a desk all day, and 16px body text that grows with
 * the window costs rows on screen without making anything easier to read.
 */
const fluid = (min: string, max: string, minRem: number, maxRem: number) => {
  // Viewport range the interpolation runs over: 22.5rem (360px) to 96rem (1536px).
  const slope = ((maxRem - minRem) / 73.5) * 100;
  const intercept = minRem - (slope / 100) * 22.5;
  return `clamp(${min}, ${intercept.toFixed(3)}rem + ${slope.toFixed(2)}vw, ${max})`;
};

export default {
  presets: [preset],
  content: ['./src/**/*.{ts,tsx,mdx}', '../../packages/ui/src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      fontSize: {
        // 18px → 22px
        'card-title': [
          fluid('1.125rem', '1.375rem', 1.125, 1.375),
          { lineHeight: '1.35', fontWeight: '600' },
        ],
        // 20px → 29px
        'section-title': [
          fluid('1.25rem', '1.8125rem', 1.25, 1.8125),
          { lineHeight: '1.3', fontWeight: '600' },
        ],
        // 26px → 40px
        'page-title': [
          fluid('1.625rem', '2.5rem', 1.625, 2.5),
          { lineHeight: '1.3', letterSpacing: '-0.01em', fontWeight: '700' },
        ],
        // 25px → 32px. Replaces the `kpi-mobile`/`kpi` pair outright — the two
        // existed only to make the step, and a KPI is the one number on a
        // dashboard that should track the space it is given.
        kpi: [fluid('1.5625rem', '2rem', 1.5625, 2), { lineHeight: '1.25', fontWeight: '700' }],
      },
    },
  },
} satisfies Config;
