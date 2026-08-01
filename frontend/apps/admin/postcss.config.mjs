export default {
  plugins: {
    // Must run before Tailwind so the shared stylesheet from @bojan/config is
    // inlined and its @layer/@apply rules get processed.
    'postcss-import': {},
    tailwindcss: {},
    autoprefixer: {},
  },
};
