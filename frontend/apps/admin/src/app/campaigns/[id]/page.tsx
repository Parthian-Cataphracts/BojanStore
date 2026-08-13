import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { AdminPage } from '@/components/AdminPage';
import { EntityForm } from '@/components/EntityForm';
import { getCampaign } from '@/lib/api/campaigns';

export const metadata: Metadata = { title: 'ویرایش کمپین' };

/** Screen 127 — ویرایش کمپین. */
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const campaign = await getCampaign(id);

  if (!campaign) notFound();

  return (
    <AdminPage
      title="ویرایش کمپین"
      breadcrumbs={[{ label: 'کمپین‌ها', href: '/campaigns' }, { label: campaign.title }]}
    >
      <EntityForm
        resource="campaigns"
        entityId={campaign.id}
        submitLabel="ذخیره کمپین"
        archive={{ noun: 'کمپین', returnTo: '/campaigns' }}
        sections={[
          {
            title: 'اطلاعات کمپین',
            icon: 'campaign',
            fields: [
              { name: 'title', label: 'عنوان کمپین', value: campaign.title, required: true },
              { name: 'description', label: 'توضیحات', kind: 'textarea', value: campaign.description ?? '' },
              {
                name: 'kind',
                label: 'نوع کمپین',
                kind: 'select',
                value: campaign.kind,
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
              { name: 'startsAt', label: 'شروع', kind: 'date', value: campaign.startsAt ?? '' },
              { name: 'endsAt', label: 'پایان', kind: 'date', value: campaign.endsAt ?? '' },
              {
                name: 'status',
                label: 'فعال باشد',
                kind: 'switch',
                checked: campaign.status === 'running',
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
