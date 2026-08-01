import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Code, Icon, buttonClasses } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { requireAdminSession } from '@/lib/auth/server';
import { adminRoles } from '@/lib/mock';

export const metadata: Metadata = { title: 'پروفایل ادمین' };

/** Screen 151 — Admin profile. */
export default async function AdminProfilePage() {
  const me = await requireAdminSession();
  const roleLabel = adminRoles.find((role) => role.id === me.role)?.name ?? me.role;

  return (
    <AdminPage
      title="پروفایل ادمین"
      breadcrumbs={[{ label: 'تنظیمات', href: '/settings' }, { label: 'پروفایل' }]}
    >
      <Card className="flex flex-wrap items-center gap-md p-lg">
        <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-soft-mint font-headline text-card-title text-primary">
          {me.name.charAt(0)}
        </span>
        <div className="flex min-w-0 flex-1 flex-col gap-xs">
          <h3 className="text-body-lg font-semibold text-primary">{me.name}</h3>
          <Code className="text-caption text-on-surface-variant">{me.email}</Code>
          <span className="tabular text-caption text-outline">نقش: {roleLabel}</span>
        </div>
      </Card>

      <div className="flex flex-wrap gap-md">
        <Link href="/settings/password" className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}>
          <Icon name="lock_reset" size={18} />
          تغییر رمز عبور
        </Link>
        <Link href="/settings/two-factor" className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}>
          <Icon name="shield" size={18} />
          تایید دو مرحله‌ای
        </Link>
      </div>

      <EntityForm
        resource="settings"
        submitLabel="ذخیره پروفایل"
        sections={[
          {
            title: 'اطلاعات شخصی',
            icon: 'person',
            fields: [
              { name: 'name', label: 'نام و نام خانوادگی', value: me.name, required: true },
              { name: 'email', label: 'ایمیل', value: me.email, latin: true },
              { name: 'phone', label: 'شماره موبایل' },
            ],
          },
        ]}
        asideSections={[
          {
            title: 'اعلان‌ها',
            icon: 'notifications',
            fields: [
              { name: 'emailAlerts', label: 'اعلان ایمیلی', kind: 'switch', checked: true },
              { name: 'smsAlerts', label: 'اعلان پیامکی', kind: 'switch' },
            ],
          },
        ]}
      />
    </AdminPage>
  );
}
