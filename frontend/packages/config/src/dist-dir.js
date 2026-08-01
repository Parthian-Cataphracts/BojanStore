/**
 * Where Next writes its build output.
 *
 * `next dev` and `next build` produce incompatible output, and by default both
 * write it to `.next`. Run one after the other and the second inherits the
 * first's leftovers — which surface as `Cannot find module
 * './vendor-chunks/…'`, or a component that is somehow "not defined" despite
 * being imported, on code that is perfectly fine. The only cure was deleting
 * the directory by hand, every time, and remembering why.
 *
 * Giving each mode its own directory removes the collision instead of cleaning
 * up after it. It is also faster than cleaning: both keep a warm cache rather
 * than rebuilding from nothing after every switch.
 *
 * `NODE_ENV` is set by the Next CLI before it loads the config — `development`
 * for `next dev`, `production` for `next build` and `next start` — so build and
 * start agree on `.next` while dev keeps to itself.
 *
 * @returns {string}
 */
export function distDir() {
  return process.env.NODE_ENV === 'development' ? '.next-dev' : '.next';
}
