'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Card, FormStatus, Icon, cn } from '@bojan/ui';
import { adminRoles, permissionSections } from '@/lib/mock';
import { postJson } from '@/lib/submit';
import type { RolePermissionDto } from '@/lib/api/types';

/**
 * Screen 146 — Role × section permission grid, now backed by
 * `GET`/`POST /admin/roles/permissions`.
 *
 * The owner row is locked: a panel with no full-access role is one bad click
 * away from being unadministrable. It is also never sent to the backend —
 * `AdminOperationsService.SaveRolePermissionsAsync` refuses a body that
 * names it, so the lock in this component and the refusal on the server
 * agree rather than one merely trusting the other.
 */
export function RolePermissionMatrix({ grants: saved }: { grants: RolePermissionDto[] }) {
  const router = useRouter();

  const [grants, setGrants] = useState<Record<string, boolean>>(() => {
    const initial: Record<string, boolean> = {};
    for (const role of adminRoles) {
      for (const section of permissionSections) {
        initial[`${role.id}:${section.key}`] =
          role.id === 'owner' ||
          saved.some((grant) => grant.role === role.id && grant.section === section.key);
      }
    }
    return initial;
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function toggle(roleId: string, section: string) {
    if (roleId === 'owner') return;
    setGrants((current) => ({
      ...current,
      [`${roleId}:${section}`]: !current[`${roleId}:${section}`],
    }));
  }

  async function save() {
    setSaving(true);
    setError(null);
    try {
      await postJson('/api/admin/roles', {
        grants: adminRoles
          .filter((role) => role.id !== 'owner')
          .flatMap((role) =>
            permissionSections.map((section) => ({
              role: role.id,
              section: section.key,
              granted: grants[`${role.id}:${section.key}`] ?? false,
            })),
          ),
      });
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره دسترسی‌ها انجام نشد.');
    } finally {
      setSaving(false);
    }
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
                <tr key={section.key} className="border-t border-outline-variant/30">
                  <th
                    scope="row"
                    className="sticky start-0 z-10 bg-surface-container-lowest px-lg py-md text-start font-normal text-on-surface"
                  >
                    {section.label}
                  </th>

                  {adminRoles.map((role) => {
                    const on = grants[`${role.id}:${section.key}`] ?? false;
                    const locked = role.id === 'owner';

                    return (
                      <td key={role.id} className="px-lg py-md text-center">
                        <button
                          type="button"
                          role="switch"
                          aria-checked={on}
                          aria-label={`${section.label} برای ${role.name}`}
                          disabled={locked}
                          onClick={() => toggle(role.id, section.key)}
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

      <FormStatus error={error} />

      <Button size="lg" loading={saving} onClick={save} className="self-start px-xl">
        ذخیره دسترسی‌ها
      </Button>
    </div>
  );
}
