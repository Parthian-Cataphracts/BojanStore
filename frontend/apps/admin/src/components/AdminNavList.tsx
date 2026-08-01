'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';
import { adminNav } from '@/lib/nav';

/**
 * The panel's navigation, rendered once and shown twice: in the fixed sidebar
 * from `md` up, and inside the mobile drawer below it. Keeping one list means a
 * new section cannot appear on desktop and go missing on a phone.
 */
export function AdminNavList({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();

  return (
    <>
      {adminNav.map((group, index) => (
        <div key={group.title ?? index} className="space-y-1">
          {group.title && <p className="px-sm pb-xs text-caption text-outline">{group.title}</p>}

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
      ))}
    </>
  );
}
