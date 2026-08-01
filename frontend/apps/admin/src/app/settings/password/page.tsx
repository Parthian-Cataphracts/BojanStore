import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { PasswordChangeForm } from '@/components/PasswordChangeForm';

export const metadata: Metadata = { title: 'تغییر رمز عبور ادمین' };

/** Screen 152 — Change admin password. */
export default function ChangePasswordPage() {
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
      <PasswordChangeForm />
    </AdminPage>
  );
}
