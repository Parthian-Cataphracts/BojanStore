'use client';

import { useState } from 'react';
import { Button, Card, Icon, cn } from '@bojan/ui';
import { adminRoles, permissionSections } from '@/lib/mock';

/**
 * Screen 146 — Role × section permission grid.
 *
 * The owner row is locked: a panel with no full-access role is one bad click
 * away from being unadministrable.
 */
export function RolePermissionMatrix() {
  /** Sections each non-owner role starts with; the owner always gets all. */
  const defaults: Record<string, string[]> = {
    product: ['محصولات', 'موجودی', 'محتوا', 'گزارش‌ها'],
    sales: ['سفارش‌ها', 'مشتریان', 'درخواست‌های سازمانی', 'گزارش‌ها'],
    support: ['سفارش‌ها', 'پشتیبانی'],
  };

  const [grants, setGrants] = useState<Record<string, boolean>>(() => {
    const initial: Record<string, boolean> = {};
    for (const role of adminRoles) {
      for (const section of permissionSections) {
        initial[`${role.id}:${section}`] =
          role.id === 'owner' || (defaults[role.id] ?? []).includes(section);
      }
    }
    return initial;
  });

  function toggle(roleId: string, section: string) {
    if (roleId === 'owner') return;
    setGrants((current) => ({
      ...current,
      [`${roleId}:${section}`]: !current[`${roleId}:${section}`],
    }));
  }

  return (
    <div className="flex flex-col gap-lg">
      <Card surface="plain" className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-start">
            <thead className="bg-surface-container-low text-on-surface-variant">
              <tr>
                <th scope="col" className="sticky start-0 z-10 bg-surface-container-low px-lg py-sm text-start">
                  بخش پنل
                </th>
                {adminRoles.map((role) => (
                  <th key={role.id} scope="col" className="px-lg py-sm text-center">
                    {role.name}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody>
              {permissionSections.map((section) => (
                <tr key={section} className="border-t border-outline-variant/30">
                  <th
                    scope="row"
                    className="sticky start-0 z-10 bg-surface-container-lowest px-lg py-md text-start font-normal text-on-surface"
                  >
                    {section}
                  </th>

                  {adminRoles.map((role) => {
                    const on = grants[`${role.id}:${section}`] ?? false;
                    const locked = role.id === 'owner';

                    return (
                      <td key={role.id} className="px-lg py-md text-center">
                        <button
                          type="button"
                          role="switch"
                          aria-checked={on}
                          aria-label={`${section} برای ${role.name}`}
                          disabled={locked}
                          onClick={() => toggle(role.id, section)}
                          className={cn(
                            'inline-flex h-8 w-8 items-center justify-center rounded-full transition-colors',
                            on ? 'bg-primary text-on-primary' : 'bg-surface-container text-outline',
                            locked && 'cursor-not-allowed opacity-70',
                          )}
                        >
                          <Icon name={on ? 'check' : 'remove'} size={18} />
                        </button>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="lock" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          دسترسی نقش «مالک» قابل تغییر نیست تا همیشه حداقل یک نقش با دسترسی کامل وجود داشته باشد.
        </p>
      </Card>

      {/*
        No backend endpoint accepts role→section grants yet — `resources.ts`
        has no `roles` entry and nothing under Administration writes them.
        Saving here would tell the operator a permission change took effect
        when nothing was ever sent, so the button stays disabled rather than
        pretend to persist local-only state.
      */}
      <Button size="lg" disabled className="self-start px-xl">
        ذخیره دسترسی‌ها
      </Button>
    </div>
  );
}
