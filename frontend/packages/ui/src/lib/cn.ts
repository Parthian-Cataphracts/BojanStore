import { clsx, type ClassValue } from 'clsx';
import { extendTailwindMerge } from 'tailwind-merge';

/*
 * tailwind-merge only knows Tailwind's stock scales. Our preset replaces the
 * spacing, type and radius scales with named tokens (`p-lg`, `text-card-title`,
 * `rounded-pill`), which it would otherwise treat as unrelated classes and keep
 * side by side — silently breaking the one thing `cn` exists for: letting a
 * caller's `className` override a component's default.
 *
 * These lists must stay in step with `@bojan/config`'s tailwind preset.
 */

const SPACING = [
  'xs',
  'sm',
  'gutter',
  'md',
  'lg',
  'xl',
  'unit',
  'safe',
  'margin-mobile',
  'margin-desktop',
];

const FONT_SIZES = [
  'headline-xl',
  'headline-lg',
  'headline-lg-mobile',
  'display-md',
  'body-lg',
  'body-md',
  'caption',
  'label-md',
  'hero',
  'hero-mobile',
  'page-title',
  'section-title',
  'card-title',
  'product-title',
  'helper',
  'kpi',
  'kpi-mobile',
  'table-header',
  'table-cell',
];

const FONT_FAMILIES = [
  'persian',
  'latin',
  'headline',
  'body',
  'display-md',
  'headline-xl',
  'headline-lg',
  'headline-lg-mobile',
  'body-lg',
  'body-md',
  'label-md',
  'caption',
];

const RADII = ['pill', '32'];

const twMerge = extendTailwindMerge({
  extend: {
    theme: {
      spacing: SPACING,
      borderRadius: RADII,
    },
    classGroups: {
      'font-size': [{ text: FONT_SIZES }],
      'font-family': [{ font: FONT_FAMILIES }],
    },
  },
});

/** Merge conditional class names, letting later Tailwind classes win. */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
