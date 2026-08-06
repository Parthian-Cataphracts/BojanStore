import type { Config } from 'tailwindcss';

/**
 * Design tokens.
 *
 * Colours and spacing are extracted verbatim from the Stitch project
 * "Bozhan Mobile Creative Retail UI" (projects/4469261971172997307),
 * design system: "Artisanal Heritage". Do not hand-edit colour values here —
 * they are the single source of truth shared by both apps.
 *
 * Typography deliberately departs from the Stitch design: the mockups used
 * Plus Jakarta Sans and Be Vietnam Pro, neither of which carries Arabic-script
 * glyphs, so Persian copy was always falling through to a fallback anyway.
 * Vazirmatn is now the primary face for the whole product and Inter is reserved
 * for Latin technical values (SKUs, API keys, webhook URLs, e-mail addresses).
 */

// Peyda, Dana and IRANSansX are not web-loaded — they are named so the stack
// picks them up when a user or a future self-hosted build provides them.
const PERSIAN = [
  'var(--font-vazirmatn)',
  'Peyda',
  'Dana',
  'IRANSansX',
  'Tahoma',
  'sans-serif',
];

const LATIN = ['var(--font-inter)', 'Arial', 'sans-serif'];

// Typed as `Partial<Config>` rather than `satisfies`: the contextual type is
// what makes the `fontSize` entries infer as tuples instead of arrays.
export const preset: Partial<Config> = {
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
      'background': '#f9f9f6',
      'error': '#ba1a1a',
      'error-container': '#ffdad6',
      'inverse-on-surface': '#f1f1ee',
      'inverse-primary': '#9acee1',
      'inverse-surface': '#2f312f',
      'on-background': '#1a1c1b',
      'on-error': '#ffffff',
      'on-error-container': '#93000a',
      'on-primary': '#ffffff',
      'on-primary-container': '#87bbce',
      'on-primary-fixed': '#001f28',
      'on-primary-fixed-variant': '#114d5d',
      'on-secondary': '#ffffff',
      'on-secondary-container': '#710f08',
      'on-secondary-fixed': '#410000',
      'on-secondary-fixed-variant': '#872016',
      'on-surface': '#1a1c1b',
      'on-surface-variant': '#40484b',
      'on-tertiary': '#ffffff',
      'on-tertiary-container': '#a2b8b4',
      'on-tertiary-fixed': '#0b1f1d',
      'on-tertiary-fixed-variant': '#374a48',
      /*
       * Darkened from #70787c, which measured 4.27:1 on the cream card
       * background — under the 4.5:1 AA needs for the 13px it is used at. It
       * carries the brand line on every product card and the search
       * placeholder, so it is small text doing real work rather than decoration.
       */
      'outline': '#5f676b',
      'outline-variant': '#c0c8cb',
      'primary': '#003441',
      'primary-container': '#0f4c5c',
      'primary-fixed': '#b6ebfe',
      'primary-fixed-dim': '#9acee1',
      'secondary': '#a8382b',
      /*
       * Darkened from #fe7765. Every surface that puts text on this colour puts
       * *white* on it — the hero's "مشاهده محصولات", the discount badge on every
       * sale card, the coral Badge, the coupon button, the filter count — and
       * white on #fe7765 measured 2.61:1, roughly four tenths of the 4.5:1 AA
       * asks for. It is the single most important button on the site.
       *
       * The palette's own `on-secondary-container` (#710f08) would have passed
       * at 4.53:1, but the button darkens to `secondary` on hover and dark text
       * is unreadable there — so the colour moves rather than the pairing, and
       * white stays legible in both states. #cc4230 is 4.78:1 against white and
       * still reads as the same coral.
       */
      'secondary-container': '#cc4230',
      'secondary-fixed': '#ffdad5',
      'secondary-fixed-dim': '#ffb4a8',
      'surface': '#f9f9f6',
      'surface-bright': '#f9f9f6',
      'surface-container': '#eeeeeb',
      'surface-container-high': '#e8e8e5',
      'surface-container-highest': '#e2e3e0',
      'surface-container-low': '#f4f4f1',
      'surface-container-lowest': '#ffffff',
      'surface-dim': '#dadad7',
      'surface-tint': '#306576',
      'surface-variant': '#e2e3e0',
      'tertiary': '#1f3330',
      'tertiary-container': '#364947',
      'tertiary-fixed': '#d1e7e3',
      'tertiary-fixed-dim': '#b5cbc7',

        // Brand aliases used throughout the design's inline styles.
      'coral': '#F36F5D',
      'deep-teal': '#003441',
      'warm-paper': '#FFF8F1',
      'soft-mint': '#DDF3EF',
      'paper-border': '#E5E0DA',
      'paper-bg': '#FAFAF7',
      },
      borderRadius: {
      'DEFAULT': '0.25rem',
      'lg': '0.5rem',
      'xl': '0.75rem',
      'full': '9999px',
      'pill': '32px',
      '32': '32px',
      },
      spacing: {
      'margin-mobile': '20px',
      'sm': '8px',
      'gutter': '16px',
      'xs': '4px',
      /**
       * The step between the two the design names.
       *
       * The source drawings are a phone and a desktop, so the gutter had only
       * those two values and jumped straight from 20px to 64px the moment `md`
       * was crossed. At a 768px viewport that is 128px — a sixth of the screen
       * — spent on empty margin, and it is what pushed the desktop header 93px
       * past the edge at exactly the width where it first appears. A tablet is
       * not a small desktop; it gets its own step.
       */
      'margin-tablet': '32px',
      'margin-desktop': '64px',
      'unit': '4px',
      'xl': '40px',
      'lg': '24px',
      'md': '16px',
      '72': '18rem',
      '20': '5rem',
      'safe': 'env(safe-area-inset-bottom)',
      },
      fontFamily: {
        // Everything is Persian by default. `latin` is opt-in, for technical
        // values only — see the `.latin` helper in styles.css.
        persian: PERSIAN,
        latin: LATIN,
        headline: PERSIAN,
        body: PERSIAN,
        // Legacy aliases kept so existing `font-*` classes keep resolving.
        'display-md': PERSIAN,
        'headline-xl': PERSIAN,
        'headline-lg': PERSIAN,
        'headline-lg-mobile': PERSIAN,
        'body-lg': PERSIAN,
        'body-md': PERSIAN,
        'label-md': PERSIAN,
        caption: PERSIAN,
      },

      /*
       * Type scale. Sizes sit mid-range of the agreed spec; pages step up on
       * `md:` where the desktop range is higher. Persian needs looser leading
       * than Latin, so body copy runs at 1.8 rather than the usual 1.5.
       */
      fontSize: {
        // Headings
        'headline-xl': ['48px', { lineHeight: '1.25', letterSpacing: '-0.02em', fontWeight: '700' }],
        'headline-lg': ['38px', { lineHeight: '1.3', letterSpacing: '-0.01em', fontWeight: '700' }],
        'headline-lg-mobile': ['26px', { lineHeight: '1.3', fontWeight: '700' }],
        'display-md': ['26px', { lineHeight: '1.3', fontWeight: '600' }],

        // Body and supporting text
        'body-lg': ['17px', { lineHeight: '1.8', fontWeight: '400' }],
        'body-md': ['16px', { lineHeight: '1.8', fontWeight: '400' }],
        caption: ['13px', { lineHeight: '1.55', fontWeight: '400' }],

        // Controls
        'label-md': ['15px', { lineHeight: '1.2', letterSpacing: '0.01em', fontWeight: '600' }],

        // Semantic aliases matching the typography spec
        hero: ['52px', { lineHeight: '1.25', letterSpacing: '-0.02em', fontWeight: '700' }],
        'hero-mobile': ['28px', { lineHeight: '1.3', fontWeight: '700' }],
        'page-title': ['40px', { lineHeight: '1.3', letterSpacing: '-0.01em', fontWeight: '700' }],
        'section-title': ['29px', { lineHeight: '1.3', fontWeight: '600' }],
        'card-title': ['20px', { lineHeight: '1.35', fontWeight: '600' }],
        'product-title': ['16px', { lineHeight: '1.5', fontWeight: '500' }],
        helper: ['12px', { lineHeight: '1.55', fontWeight: '400' }],

        // Admin-specific
        kpi: ['32px', { lineHeight: '1.25', fontWeight: '700' }],
        'kpi-mobile': ['25px', { lineHeight: '1.25', fontWeight: '700' }],
        'table-header': ['14px', { lineHeight: '1.5', fontWeight: '600' }],
        'table-cell': ['14px', { lineHeight: '1.5', fontWeight: '400' }],
      },
      boxShadow: {
        // .soft-shadow / .ambient-shadow from the design
        soft: '0 16px 32px rgba(0, 52, 65, 0.04)',
        ambient: '0 8px 24px rgba(0, 52, 65, 0.04)',
      },
      maxWidth: {
        shell: '1280px',
        content: '1200px',
      },
      backdropBlur: {
        nav: '10px',
        panel: '12px',
      },
    },
  },
};

export default preset;
