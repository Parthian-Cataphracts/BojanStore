import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';

export const metadata: Metadata = { title: 'افزودن کمپین' };

/** Screen 127 — Create a campaign. */
export default function NewCampaignPage() {
  return (
    <AdminPage
      title="افزودن کمپین"
      breadcrumbs={[{ label: 'کمپین‌ها', href: '/campaigns' }, { label: 'افزودن کمپین' }]}
    >
      <EntityForm
        resource="campaigns"
        submitLabel="ذخیره کمپین"
        sections={[
          {
            title: 'اطلاعات کمپین',
            icon: 'campaign',
            fields: [
              { name: 'title', label: 'عنوان کمپین', required: true },
              { name: 'description', label: 'توضیحات', kind: 'textarea' },
              {
                name: 'kind',
                label: 'نوع کمپین',
                kind: 'select',
                required: true,
                options: [
                  { value: 'discount', label: 'تخفیف' },
                  { value: 'banner', label: 'بنر' },
                  { value: 'email', label: 'ایمیل' },
                  { value: 'sms', label: 'پیامک' },
                ],
              },
            ],
          },
        ]}
        asideSections={[
          {
            title: 'زمان‌بندی',
            icon: 'event',
            fields: [
              { name: 'startsAt', label: 'شروع', kind: 'date' },
              { name: 'endsAt', label: 'پایان', kind: 'date' },
              {
                name: 'status',
                label: 'فعال باشد',
                kind: 'switch',
                checked: true,
                onValue: 'running',
                offValue: 'scheduled',
              },
            ],
          },
        ]}
      />
    </AdminPage>
  );
}
