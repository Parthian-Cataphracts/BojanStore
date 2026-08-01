import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { ApiSettingsPanel } from '@/components/ApiSettingsPanel';
import { getApiKeys } from '@/lib/api/settings';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'تنظیمات API و وبهوک' };

/** Screen 155 — API keys and webhooks. */
export default async function ApiSettingsPage() {
  await requireRole('owner');
  const keys = await getApiKeys();

  return (
    <AdminPage
      title="تنظیمات API و وبهوک"
      description="کلیدهای دسترسی و آدرس‌هایی که رویدادهای فروشگاه به آن‌ها ارسال می‌شود."
      breadcrumbs={[{ label: 'تنظیمات', href: '/settings' }, { label: 'API و وبهوک' }]}
    >
      <ApiSettingsPanel keys={keys} />
    </AdminPage>
  );
}
