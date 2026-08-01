/**
 * Shared PostCSS config.
 *
 * `postcss-import` must come first: the apps' `globals.css` only does
 * `@import '@bojan/config/styles.css'`, and Tailwind has to see the inlined
 * `@tailwind` / `@layer` / `@apply` rules.
 */
export default {
  plugins: {
    'postcss-import': {},
    tailwindcss: {},
    autoprefixer: {},
  },
};
