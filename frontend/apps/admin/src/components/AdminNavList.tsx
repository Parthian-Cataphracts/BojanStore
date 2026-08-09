'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';
import { adminNav } from '@/lib/nav';
import { useAdminRole } from '@/lib/admin-role';

const STORAGE_KEY = 'bojan-admin-nav-collapsed';

function groupKey(title: string | undefined, index: number): string {
  return title ?? `group-${index}`;
}

function groupContainsPath(items: { href: string }[], pathname: string): boolean {
  return items.some((item) => (item.href === '/' ? pathname === '/' : pathname.startsWith(item.href)));
}

/**
 * The panel's navigation, rendered once and shown three times: in the fixed
 * sidebar from `md` up (full or as an icon-only rail — see `collapsed`),
 * inside the mobile drawer below it, and nowhere else. Keeping one list means
 * a new section cannot appear on desktop and go missing on a phone.
 *
 * Titled groups collapse independently of the rail — the panel has six of
 * them and a screen short enough to need a drawer at all is short enough
 * that all six open at once push the group an operator actually wants below
 * the fold. The group holding the current page always renders open
 * regardless of its saved state, so navigating somewhere never hides where
 * you just went. That per-group state is moot in rail mode, which always
 * shows every icon — a collapsed group with no visible label would just be
 * a mystery gap in the rail.
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
  const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>({});

  useEffect(() => {
    try {
      const saved = window.localStorage.getItem(STORAGE_KEY);
      if (saved) setCollapsedGroups(JSON.parse(saved) as Record<string, boolean>);
    } catch {
      // A corrupt or missing value just means every group starts open.
    }
  }, []);

  function toggleGroup(key: string) {
    setCollapsedGroups((current) => {
      const next = { ...current, [key]: !current[key] };
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      } catch {
        // Persistence is a convenience, not a requirement — the toggle still works this session.
      }
      return next;
    });
  }

  return (
    <>
      {adminNav.map((group, index) => {
        const key = groupKey(group.title, index);
        const hasActiveItem = groupContainsPath(group.items, pathname);
        const isOpen = collapsed || hasActiveItem || !collapsedGroups[key];

        return (
          <div key={key} className="space-y-1">
            {group.title && !collapsed && (
              <button
                type="button"
                onClick={() => toggleGroup(key)}
                aria-expanded={isOpen}
                aria-controls={`nav-group-${key}`}
                className="flex w-full items-center justify-between rounded-lg px-sm pb-xs pt-1 text-caption text-outline transition-colors hover:text-primary"
              >
                <span>{group.title}</span>
                <Icon
                  name="expand_more"
                  size={18}
                  className={cn('transition-transform duration-200', !isOpen && '-rotate-90')}
                />
              </button>
            )}

            <div id={`nav-group-${key}`} hidden={!isOpen} className="space-y-1">
              {group.items.map((item) => {
                const active = item.href === '/' ? pathname === '/' : pathname.startsWith(item.href);
                // The screen enforces this itself and will send the operator to
                // `/forbidden`; the nav's job is only to say so beforehand.
                const locked = item.roles !== undefined && role !== null && !item.roles.includes(role);
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
                      'flex items-center gap-md rounded-lg transition-colors',
                      collapsed ? 'justify-center px-0 py-3' : 'px-sm py-3',
                      active
                        ? collapsed
                          ? 'bg-surface-container-low font-semibold text-primary'
                          : 'rounded-e-none border-e-4 border-secondary bg-surface-container-low font-semibold text-primary'
                        : 'font-medium text-on-surface-variant hover:bg-surface-container-low hover:text-primary',
                    )}
                  >
                    <Icon name={item.icon} filled={active} />
                    {!collapsed && <span className="text-body-md">{item.label}</span>}
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
                      <Icon name="lock" size={16} className="ms-auto shrink-0 text-outline" />
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
