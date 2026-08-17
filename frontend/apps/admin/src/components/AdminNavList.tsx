'use client';

import { useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';
import { adminNav } from '@/lib/nav';
import { canOpen } from '@/lib/permissions';
import { useAdminGrants, useAdminRole } from '@/lib/admin-role';

function groupKey(title: string | undefined, index: number): string {
  return title ?? `group-${index}`;
}

/**
 * The one entry that is the current page, or null on a screen no entry names.
 *
 * The **longest** matching href wins, and that is the whole point of computing
 * this once for the tree rather than asking each item whether the path starts
 * with its own href. Nine of these entries are a prefix of another one, so the
 * per-item test lit several rows at a time: on «پرداخت و درگاه» the settings
 * group also lit «اطلاعات فروشگاه» (`/settings` is a prefix of everything under
 * it), on «هشدار کمبود» it lit «موجودی و انبار», and on «پروفایل من» it marked
 * two different groups as holding the current page. A menu that highlights four
 * rows has told the operator nothing about which one they are on.
 *
 * A detail screen still resolves to its list — `/orders/BZ-1` has no entry of
 * its own and `/orders` is the longest href that matches it, which is the
 * behaviour the prefix test was there for in the first place.
 */
function activeHrefFor(groups: { items: { href: string }[] }[], pathname: string): string | null {
  let best: string | null = null;

  for (const group of groups) {
    for (const { href } of group.items) {
      const matches =
        href === '/' ? pathname === '/' : pathname === href || pathname.startsWith(`${href}/`);
      if (matches && (best === null || href.length > best.length)) best = href;
    }
  }

  return best;
}

/**
 * The panel's navigation, rendered once and shown twice: in the fixed sidebar
 * from `md` up, and inside the mobile drawer below it. Keeping one list means a
 * new section cannot appear on desktop and go missing on a phone.
 *
 * ## Accordion
 *
 * Titled groups are a true accordion: **at most one is open at a time**, and on
 * every load **none of them is**. Both halves of that are deliberate.
 *
 * Ten groups that each remember their own boolean is not a menu, it is ten
 * menus — the panel used to open every group at once and put forty-odd
 * destinations in a scrolling column, where finding «پیامک» meant reading past
 * «مرجوعی‌ها». One open group is a short list with a heading above it.
 *
 * Nothing opens a group except the operator clicking it. In particular the
 * group holding the current page does *not* spring open on first render: the
 * sidebar would then look different on a reload than it did a moment earlier,
 * and «why is تنظیمات open?» is a question the answer to which is invisible.
 * What the current page gets instead is a mark on its group's heading — the
 * group says it holds where you are without taking the screen to say it.
 *
 * State is not persisted. A closed menu is the resting state of this panel, so
 * there is nothing about the last session worth restoring into it.
 *
 * ## Rail
 *
 * `collapsed` is the desktop icon rail and is a separate concept from the
 * accordion — see `AdminShell`. A rail has no headings to click, so it shows
 * every destination as an icon and leaves the accordion state alone; expanding
 * the sidebar again finds the groups exactly as they were.
 */
export function AdminNavList({
  onNavigate,
  collapsed = false,
}: {
  onNavigate?: () => void;
  /** Icon-only rail — set only by the fixed desktop sidebar, never the mobile drawer. */
  collapsed?: boolean;
}) {
  const pathname = usePathname();
  const role = useAdminRole();
  const grants = useAdminGrants();
  const [openGroup, setOpenGroup] = useState<string | null>(null);

  /*
    The menu this operator actually has.

    Left out, not drawn with a lock — which is the opposite of what the `roles`
    entries below do, and the difference is worth stating. A role is a job
    title: everybody in a shop knows the backup screen belongs to the owner, so
    showing it locked tells a product manager something true about how the panel
    is arranged. A grant is a decision somebody made about one person. Drawing
    «پرداخت و درگاه» locked to the one salesperson who was not given it
    advertises what they were refused, on every page, to whoever is looking at
    their screen — and there is nothing they can do about it from here anyway.

    Groups follow their contents: a group with nothing left in it is a heading
    that opens onto nothing, so it goes too.
  */
  const visibleNav = adminNav
    .map((group) => ({ ...group, items: group.items.filter((item) => canOpen(grants, item)) }))
    .filter((group) => group.items.length > 0);

  const activeHref = activeHrefFor(visibleNav, pathname);

  function toggleGroup(key: string) {
    // The whole accordion in one line: opening a group closes whichever was
    // open, and clicking the open one closes it and leaves nothing open.
    setOpenGroup((current) => (current === key ? null : key));
  }

  return (
    <>
      {visibleNav.map((group, index) => {
        const key = groupKey(group.title, index);
        const untitled = !group.title;
        // An untitled group is a lone top-level link — the dashboard — with
        // nothing to collapse. The rail has no headings at all, so there too
        // every item is simply drawn.
        const isOpen = untitled || collapsed || openGroup === key;
        const holdsCurrentPage = group.items.some((item) => item.href === activeHref);

        return (
          <div
            key={key}
            className={cn(
              // A rail separates its groups with a hairline instead of a
              // heading, so the reader can still tell where کاتالوگ ends.
              collapsed && index > 0 && 'border-outline-variant/50 pt-xs border-t',
            )}
          >
            {group.title && !collapsed && (
              <button
                type="button"
                onClick={() => toggleGroup(key)}
                aria-expanded={isOpen}
                aria-controls={`nav-group-${key}`}
                className={cn(
                  'gap-sm px-sm text-caption flex w-full items-center rounded-lg py-2 transition-colors',
                  'hover:bg-surface-container-low focus-visible:ring-primary/40 focus-visible:outline-none focus-visible:ring-2',
                  isOpen || holdsCurrentPage
                    ? 'text-primary font-semibold'
                    : 'text-on-surface-variant font-medium',
                )}
              >
                {/*
                  The mark that replaces auto-opening: a dot on the heading of
                  the group holding the current page. It is drawn while the
                  group is closed only — once it is open the active item inside
                  is saying the same thing, and two indicators for one fact read
                  as two facts.
                */}
                <span
                  aria-hidden="true"
                  className={cn(
                    'h-1.5 w-1.5 shrink-0 rounded-full transition-colors',
                    holdsCurrentPage && !isOpen ? 'bg-secondary' : 'bg-transparent',
                  )}
                />
                <span className="truncate">{group.title}</span>
                <Icon
                  name="expand_more"
                  size={18}
                  className={cn(
                    'text-outline ms-auto shrink-0 transition-transform duration-200',
                    !isOpen && '-rotate-90',
                  )}
                />
              </button>
            )}

            <div
              id={`nav-group-${key}`}
              hidden={!isOpen}
              className={cn(
                'space-y-px',
                // Children of an open group hang off a hairline on the start
                // edge, which is what makes them read as belonging to the
                // heading above rather than as more top-level entries.
                group.title && !collapsed && 'ms-sm mt-xs border-outline-variant/60 ps-xs border-s',
              )}
            >
              {group.items.map((item) => {
                const active = item.href === activeHref;
                // The screen enforces this itself and will send the operator to
                // `/forbidden`; the nav's job is only to say so beforehand.
                const locked =
                  item.roles !== undefined && role !== null && !item.roles.includes(role);
                const hint = locked
                  ? `${item.label} — این بخش فقط برای نقش‌های مجاز باز می‌شود`
                  : collapsed
                    ? item.label
                    : undefined;

                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={onNavigate}
                    aria-current={active ? 'page' : undefined}
                    title={hint}
                    className={cn(
                      'flex items-center rounded-lg transition-colors',
                      'focus-visible:ring-primary/40 focus-visible:outline-none focus-visible:ring-2',
                      collapsed ? 'justify-center gap-0 px-0 py-2.5' : 'gap-sm px-sm py-2',
                      active
                        ? 'bg-surface-container text-primary font-semibold'
                        : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary font-medium',
                    )}
                  >
                    {/*
                      The active accent, and the reason it is a span rather than
                      a border: a border on the item changes its box, so the
                      label of the active row sat a pixel off from its
                      neighbours. This occupies the same 2px whether or not it
                      is drawn.
                    */}
                    {!collapsed && (
                      <span
                        aria-hidden="true"
                        className={cn(
                          'h-5 w-0.5 shrink-0 rounded-full transition-colors',
                          active ? 'bg-secondary' : 'bg-transparent',
                        )}
                      />
                    )}
                    <Icon name={item.icon} filled={active} size={collapsed ? 24 : 20} />
                    {!collapsed && <span className="text-body-md truncate">{item.label}</span>}
                    {/*
                      Marked rather than filtered out. An operator who cannot
                      open the backup screen is better served knowing it exists
                      and is not theirs than by a nav that quietly differs from
                      the one a colleague is describing to them over the phone —
                      which is the same reasoning the wallet entry already
                      carried, now applied to every gated section instead of
                      being explained in a comment beside one of them.
                    */}
                    {locked && !collapsed && (
                      <Icon name="lock" size={15} className="text-outline ms-auto shrink-0" />
                    )}
                  </Link>
                );
              })}
            </div>
          </div>
        );
      })}
    </>
  );
}
