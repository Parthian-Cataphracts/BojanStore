import { routes } from './routes';

/**
 * Which routes go without the shop's furniture.
 *
 * Signing in, registering and recovering a password are single-task screens. A
 * footer full of shipping terms and a tab bar offering four other places to go
 * are exit ramps from the one thing the customer came to do, and on a phone
 * they take a third of the screen the form needs. So these routes get neither.
 *
 * The rule lives here rather than inside `ShopChrome` because two components
 * need the same answer and would otherwise each keep their own copy: the
 * chrome, to decide whether to render the bar, and the chat launcher, to decide
 * whether there is a bar above which to float. A launcher that assumed one was
 * always there floated 64px off the bottom of the sign-in screen, over nothing.
 */
const BARE_ROUTES = [
  routes.login,
  routes.register,
  routes.forgotPassword,
  routes.resetPassword,
];

/** Matched on segment boundaries, so `/logins` is not `/login`. */
export function isBareRoute(pathname: string): boolean {
  return BARE_ROUTES.some((route) => pathname === route || pathname.startsWith(`${route}/`));
}

/**
 * One product, on a phone: the screen keeps the shop's furniture at desktop
 * widths and drops it below them.
 *
 * A product page on a phone is already carrying a picture, a price, a variant
 * picker and a buy bar pinned to the bottom edge. Under that bar sat the tab
 * bar, and over both floated the chat launcher — two rows of chrome and a
 * button between the shopper and the only control the screen is for. The large
 * stores all do the same thing here: the page takes the whole screen and a back
 * arrow over the image is the way out.
 *
 * Only the detail page itself. `/products` is a list, and a list is exactly
 * where the tab bar earns its place; the gallery, reviews and questions are
 * their own screens and keep theirs.
 *
 * Read by three: the chrome, to leave the tab bar off; the chat launcher, to
 * stay out of the way; and `StickyActionBar`, to sit on the bottom edge rather
 * than on a bar that is not there. One rule, because three copies of it is how
 * a bar comes back on one of them.
 */
const PRODUCT_DETAIL = /^\/products\/[^/]+$/;

export function isImmersiveRoute(pathname: string): boolean {
  return PRODUCT_DETAIL.test(pathname);
}
