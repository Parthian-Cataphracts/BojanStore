/**
 * Non-Tailwind design constants shared by both apps.
 * Mirrors the "Artisanal Heritage" design system from the Stitch project.
 */

export const brand = {
  coral: '#F36F5D',
  deepTeal: '#003441',
  warmPaper: '#FFF8F1',
  softMint: '#DDF3EF',
  paperBorder: '#E5E0DA',
  paperBg: '#FAFAF7',
} as const;

/** Design canvas widths the screens were drawn at. */
export const breakpoints = {
  mobile: 390,
  tablet: 768,
  desktop: 1280,
} as const;

/** Layout rails taken from the design's `px-margin-*` usage. */
export const layout = {
  shellMaxWidth: 1280,
  contentMaxWidth: 1200,
  headerHeight: 80,
  mobileHeaderHeight: 64,
  bottomNavHeight: 72,
} as const;

export type Brand = typeof brand;
