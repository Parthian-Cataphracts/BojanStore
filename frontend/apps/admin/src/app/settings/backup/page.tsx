import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { BackupPanel } from '@/components/BackupPanel';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'پشتیبان‌گیری و بازیابی اطلاعات' };

/** Screen 156 — Backup and restore. */
export default async function BackupPage() {
  await requireRole('owner');
  return (
    <AdminPage
      title="پشتیبان‌گیری و بازیابی"
      description="از داده‌های فروشگاه نسخه پشتیبان بگیرید یا نسخه‌ای را بازگردانید."
      breadcrumbs={[{ label: 'تنظیمات', href: '/settings' }, { label: 'پشتیبان‌گیری' }]}
    >
      <BackupPanel />
    </AdminPage>
  );
}
