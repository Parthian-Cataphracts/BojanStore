import type { Metadata } from 'next';
import { Card, Icon } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { PasswordChangeForm } from '@/components/PasswordChangeForm';
import { requireAdminSession } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'تغییر رمز عبور ادمین' };

/** Screen 152 — Change admin password. */
export default async function ChangePasswordPage() {
  const session = await requireAdminSession();

  return (
    <AdminPage
      title="تغییر رمز عبور"
      description="پس از تغییر رمز، همه نشست‌های فعال شما روی دستگاه‌های دیگر بسته می‌شود."
      breadcrumbs={[
        { label: 'تنظیمات', href: '/settings' },
        { label: 'پروفایل', href: '/settings/profile' },
        { label: 'تغییر رمز عبور' },
      ]}
    >
      {/*
        The one screen an operator with this flag can reach, so it is also the
        only place that can tell them why. Without it they arrive here from
        whatever they clicked, with no explanation and no way onward — which
        reads as a broken panel rather than as a rule.
      */}
      {session.mustChangePassword && (
        <Card className="flex items-start gap-sm border-secondary/40 p-md">
          <Icon name="lock_clock" size={20} className="mt-px shrink-0 text-secondary" />
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            رمز فعلی شما را شخص دیگری تعیین کرده است. تا وقتی رمز خودتان را انتخاب نکنید بقیه‌ی
            بخش‌های پنل باز نمی‌شود — چون رمزی که دو نفر می‌دانند، رمز هیچ‌کس نیست.
          </p>
        </Card>
      )}

      <PasswordChangeForm />
    </AdminPage>
  );
}
