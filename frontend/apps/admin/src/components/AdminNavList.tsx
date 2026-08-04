'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';
import { adminNav } from '@/lib/nav';

const STORAGE_KEY = 'bojan-admin-nav-collapsed';

function groupKey(title: string | undefined, index: number): string {
  return title ?? `group-${index}`;
}

function groupContainsPath(items: { href: string }[], pathname: string): boolean {
  return items.some((item) => (item.href === '/' ? pathname === '/' : pathname.startsWith(item.href)));
}

/**
 * The panel's navigation, rendered once and shown twice: in the fixed sidebar
 * from `md` up, and inside the mobile drawer below it. Keeping one list means a
 * new section cannot appear on desktop and go missing on a phone.
 *
 * Titled groups collapse — the panel has six of them and a screen short
 * enough to need a drawer at all is short enough that all six open at once
 * push the group an operator actually wants below the fold. The group holding
 * the current page always renders open regardless of its saved state, so
 * navigating somewhere never hides where you just went.
 */
export function AdminNavList({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  useEffect(() => {
    try {
      const saved = window.localStorage.getItem(STORAGE_KEY);
      if (saved) setCollapsed(JSON.parse(saved) as Record<string, boolean>);
    } catch {
      // A corrupt or missing value just means every group starts open.
    }
  }, []);

  function toggle(key: string) {
    setCollapsed((current) => {
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
        const isOpen = hasActiveItem || !collapsed[key];

        return (
          <div key={key} className="space-y-1">
            {group.title && (
              <button
                type="button"
                onClick={() => toggle(key)}
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

                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={onNavigate}
                    aria-current={active ? 'page' : undefined}
                    className={cn(
                      'flex items-center gap-md rounded-lg px-sm py-3 transition-colors',
                      active
                        ? 'rounded-e-none border-e-4 border-secondary bg-surface-container-low font-semibold text-primary'
                        : 'font-medium text-on-surface-variant hover:bg-surface-container-low hover:text-primary',
                    )}
                  >
                    <Icon name={item.icon} filled={active} />
                    <span className="text-body-md">{item.label}</span>
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
