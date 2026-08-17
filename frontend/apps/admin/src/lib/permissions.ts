// Relative rather than `@/lib/nav`: the workspace's vitest config points `@/`
// at the storefront, so a unit test importing this module would resolve the
// menu to the other application's. `nav` has no runtime imports of its own, so
// this one path is all it takes to make the catalogue testable.
import { adminNav, type AdminNavItem } from './nav';

/**
 * What an operator may open, and how the panel decides.
 *
 * ## Two grains
 *
 * A grant is a **section** — one of the ten coarse areas the API gates on — or
 * a single **screen** inside one, keyed by its own route. Both live in the same
 * list, because an owner ticking «سفارش‌ها» and an owner ticking only
 * «مرجوعی‌ها» are answering the same question at two levels of detail, and the
 * screen was unusable at the coarse one alone: returns and orders are one
 * section, so somebody who should only handle returns had to be handed the
 * order queue with them.
 *
 * Holding a section is holding every screen under it. That is what the rows
 * written before screens existed mean, and it stays the way a whole area is
 * granted in one tick rather than eight.
 *
 * ## Empty means unnarrowed
 *
 * An operator with no grants at all reaches whatever their role allows. That is
 * the state somebody is in the moment they are appointed, and reading it as
 * «nothing» would leave every new operator staring at an empty menu until an
 * owner went and ticked boxes. The API's own filter reads it the same way — see
 * `SectionPermissionFilter`.
 *
 * ## Owners
 *
 * Never narrowed, here or anywhere. The API refuses to store grants against an
 * owner and refuses to check them, and this refuses to hide anything from one.
 *
 * ## Where this is enforced
 *
 * Three places, and they are not the same strength. The menu leaves out what an
 * operator cannot open; the middleware turns away a typed address; the API
 * refuses the request underneath. Only the last of those is a wall — but the
 * API gates at section granularity, so an operator granted «مرجوعی‌ها» alone
 * can still be served order data by a request they have no screen to make. What
 * the fine grain buys is a panel that shows one person their job and not
 * somebody else's, which is what was asked for; it is not a claim that the
 * order rows are cryptographically out of their reach.
 */

/** The ten coarse areas, in the order the checklist draws them. */
export const SECTIONS = [
  { key: 'orders', label: 'سفارش‌ها' },
  { key: 'products', label: 'محصولات' },
  { key: 'inventory', label: 'موجودی' },
  { key: 'customers', label: 'مشتریان' },
  { key: 'business', label: 'درخواست‌های سازمانی' },
  { key: 'content', label: 'محتوا' },
  { key: 'campaigns', label: 'کمپین‌ها' },
  { key: 'support', label: 'پشتیبانی' },
  { key: 'reports', label: 'گزارش‌ها' },
  { key: 'settings', label: 'تنظیمات' },
] as const;

export type SectionKey = (typeof SECTIONS)[number]['key'];

export const sectionLabel = (key: string): string =>
  SECTIONS.find((section) => section.key === key)?.label ?? key;

/** One grantable screen: its route, what it is called, and the section it sits in. */
export interface AssignableScreen {
  key: string;
  label: string;
  section: SectionKey;
}

/**
 * Every screen an owner can grant, grouped by section — derived from the menu
 * rather than listed again.
 *
 * The menu is the only place that knows a screen exists, so a second list would
 * be one a new screen gets left out of: it would appear in the nav, be
 * ungrantable on this screen, and therefore be invisible to every narrowed
 * operator with no way for an owner to fix it. Items with no `section` — the
 * dashboard, and the three screens about the operator's own account — are not
 * permissions at all and are left out here.
 */
export function assignableScreens(): {
  section: SectionKey;
  label: string;
  screens: AssignableScreen[];
}[] {
  const bySection = new Map<SectionKey, AssignableScreen[]>();

  for (const group of adminNav) {
    for (const item of group.items) {
      if (!item.section) continue;
      const screens = bySection.get(item.section) ?? [];
      screens.push({ key: item.href, label: item.label, section: item.section });
      bySection.set(item.section, screens);
    }
  }

  return SECTIONS.filter((section) => bySection.has(section.key)).map((section) => ({
    section: section.key,
    label: section.label,
    screens: bySection.get(section.key) ?? [],
  }));
}

/**
 * Whether these grants open that nav entry.
 *
 * `null` grants is an owner or a session minted before grants were carried;
 * both mean "not narrowed". An entry with no section — the dashboard, the
 * operator's own account screens — is everybody's.
 */
export function canOpen(grants: readonly string[] | null, item: AdminNavItem): boolean {
  if (!item.section) return true;
  if (grants === null || grants.length === 0) return true;
  return grants.includes(item.section) || grants.includes(item.href);
}

/**
 * The nav entry a path belongs to, by longest matching href.
 *
 * Longest wins for the reason it does in the sidebar's active state: nine of
 * these entries are a prefix of another, so `/settings/logs` matched
 * `/settings` first and an operator granted the shop's settings would have been
 * handed the server log with them.
 */
export function navItemFor(pathname: string): AdminNavItem | null {
  let best: AdminNavItem | null = null;

  for (const group of adminNav) {
    for (const item of group.items) {
      const matches =
        item.href === '/'
          ? pathname === '/'
          : pathname === item.href || pathname.startsWith(`${item.href}/`);
      if (matches && (best === null || item.href.length > best.href.length)) best = item;
    }
  }

  return best;
}

/** Whether these grants admit this path. Paths no entry names are left alone. */
export function canOpenPath(grants: readonly string[] | null, pathname: string): boolean {
  const item = navItemFor(pathname);
  return item === null || canOpen(grants, item);
}

/**
 * Whether these grants reach a section at all — the whole of it, or any one
 * screen in it.
 *
 * The dashboard asks this. It is nobody's permission and always in the menu,
 * but the figures on it come from the reports endpoints, so an operator with no
 * claim on those lands on a page of failed panels. Screen-level rather than
 * section-level on purpose: somebody granted one report has been given a reason
 * to look at the dashboard.
 */
export function reachesSection(grants: readonly string[] | null, section: SectionKey): boolean {
  if (grants === null || grants.length === 0) return true;
  return grants.some((grant) => grant === section || screenSection(grant) === section);
}

/** The section a stored grant sits in — the API's `PanelScreen.SectionOf`, this side. */
function screenSection(grant: string): SectionKey | null {
  for (const section of assignableScreens()) {
    if (section.screens.some((screen) => screen.key === grant)) return section.section;
  }
  return null;
}

/**
 * The first screen this operator can actually open, for when the one they asked
 * for is not theirs.
 *
 * In menu order, so it is the top of their own sidebar rather than an arbitrary
 * one. The account screens are the floor and cannot be taken away, so this
 * always has an answer.
 */
export function firstOpenPath(grants: readonly string[] | null): string {
  for (const group of adminNav) {
    for (const item of group.items) {
      if (item.href !== '/' && canOpen(grants, item)) return item.href;
    }
  }

  return '/settings/profile';
}
