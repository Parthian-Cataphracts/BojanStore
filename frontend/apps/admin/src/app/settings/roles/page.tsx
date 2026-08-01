import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { RolePermissionMatrix } from '@/components/RolePermissionMatrix';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'نقش‌ها و دسترسی‌ها' };

/** Screen 146 — Roles and permissions. */
export default async function RolesPage() {
  await requireRole('owner');
  return (
    <AdminPage
      title="نقش‌ها و دسترسی‌ها"
      description="تعیین کنید هر نقش به کدام بخش‌های پنل دسترسی داشته باشد."
      breadcrumbs={[{ label: 'تنظیمات', href: '/settings' }, { label: 'نقش‌ها و دسترسی‌ها' }]}
    >
      <RolePermissionMatrix />
    </AdminPage>
  );
}
