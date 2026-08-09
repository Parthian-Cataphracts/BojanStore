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
