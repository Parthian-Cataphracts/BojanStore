import Link from 'next/link';
import { cn } from '@bojan/ui';
import { routes } from '@/lib/routes';

/**
 * The sign-in / register pair, as two links rather than a client-side toggle.
 *
 * Two screens with two addresses, so a customer can be sent straight to either
 * one, the browser's back button does what it looks like it does, and "?next="
 * survives the move between them. A toggle would make registering a state this
 * page happened to be in, which is not something you can link to.
 */
export function AuthSwitch({ active, next }: { active: 'login' | 'register'; next?: string | null }) {
  const query = next ? `?next=${encodeURIComponent(next)}` : '';

  const tabs = [
    { id: 'login' as const, label: 'ورود', href: `${routes.login}${query}` },
    { id: 'register' as const, label: 'ثبت‌نام', href: `${routes.register}${query}` },
  ];

  return (
    <div
      role="tablist"
      aria-label="ورود یا ثبت‌نام"
      className="mb-lg grid grid-cols-2 gap-xs rounded-full bg-surface-container-low p-xs"
    >
      {tabs.map((tab) => {
        const current = tab.id === active;
        return (
          <Link
            key={tab.id}
            href={tab.href}
            role="tab"
            aria-selected={current}
            aria-current={current ? 'page' : undefined}
            className={cn(
              'rounded-full py-sm text-center text-label-md font-label-md transition-colors',
              current
                ? 'bg-primary text-on-primary shadow-soft'
                : 'text-on-surface-variant hover:text-primary',
            )}
          >
            {tab.label}
          </Link>
        );
      })}
    </div>
  );
}
