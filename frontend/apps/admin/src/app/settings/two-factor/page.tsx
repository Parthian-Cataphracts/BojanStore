import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { TwoFactorSetup } from '@/components/TwoFactorSetup';

export const metadata: Metadata = { title: 'تایید دو مرحله‌ای ادمین' };

/** Screen 153 — Two-factor authentication. */
export default function TwoFactorPage() {
  return (
    <AdminPage
      title="تایید دو مرحله‌ای"
      description="با فعال کردن این گزینه، ورود به پنل علاوه بر رمز عبور به کد یک‌بارمصرف هم نیاز دارد."
      breadcrumbs={[
        { label: 'تنظیمات', href: '/settings' },
        { label: 'پروفایل', href: '/settings/profile' },
        { label: 'تایید دو مرحله‌ای' },
      ]}
    >
      <TwoFactorSetup />
    </AdminPage>
  );
}
