import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Badge, Card, Code, Icon, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { FormSection } from '@/components/FormLayout';
import { BusinessRequestActions } from '@/components/BusinessRequestActions';
import { getBusinessRequests } from '@/lib/api/business-requests';
import { getAdminUsers } from '@/lib/api/settings';

export const metadata: Metadata = { title: 'جزئیات درخواست سازمانی' };

const statusMeta = {
  submitted: { label: 'ثبت شده', tone: 'neutral' as const },
  reviewing: { label: 'در حال بررسی', tone: 'warning' as const },
  quoted: { label: 'پیش‌فاکتور صادر شد', tone: 'mint' as const },
  approved: { label: 'تایید شده', tone: 'teal' as const },
  rejected: { label: 'رد شده', tone: 'error' as const },
};

/** Screen 120 — Business request detail in the admin panel. */
export default async function BusinessRequestDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const [{ items }, { items: admins }] = await Promise.all([
    getBusinessRequests({ pageSize: 200 }),
    getAdminUsers({ pageSize: 200 }),
  ]);
  const request = items.find((item) => item.id === id);
  if (!request) notFound();

  const status = statusMeta[request.status as keyof typeof statusMeta];

  // Only active operators can take an assignment.
  const specialists = admins
    .filter((user) => user.status === 'active')
    .map((user) => ({ id: user.id, name: user.name }));

  return (
    <AdminPage
      title="جزئیات درخواست سازمانی"
      breadcrumbs={[
        { label: 'درخواست‌های سازمانی', href: '/business-requests' },
        { label: request.code },
      ]}
      actions={
        <BusinessRequestActions requestId={request.id} specialists={specialists} slot="header" />
      }
    >
      <Card className="flex flex-wrap items-start justify-between gap-md p-lg">
        <div className="flex flex-col gap-xs">
          <span className="text-caption text-on-surface-variant">شناسه درخواست</span>
          <Code className="text-body-lg font-semibold text-primary">#{request.code}</Code>
        </div>
        <Badge tone={status.tone}>{status.label}</Badge>
      </Card>

      <div className="grid gap-lg lg:grid-cols-[1fr_320px] lg:items-start">
        {/* See the order screen: a grid item defaults to `min-width: auto` and
            will not shrink under its widest child, so anything that scrolls
            sideways stretches the page instead. */}
        <div className="flex min-w-0 flex-col gap-lg">
          <FormSection title="اطلاعات سازمان" icon="domain">
            <dl className="flex flex-col gap-md">
              {[
                { label: 'نام سازمان', value: request.organization },
                { label: 'فرد رابط', value: request.contact },
                { label: 'تعداد اقلام', value: `${toPersianDigits(request.itemCount)} قلم` },
                { label: 'تاریخ ثبت', value: formatDate(request.createdAt, 'long') },
              ].map((row) => (
                <div
                  key={row.label}
                  className="flex items-center justify-between gap-md border-b border-paper-border pb-sm last:border-0 last:pb-0"
                >
                  <dt className="text-caption text-on-surface-variant">{row.label}</dt>
                  <dd className="tabular text-body-md text-on-surface">{row.value}</dd>
                </div>
              ))}
            </dl>
          </FormSection>

          <FormSection title="یادداشت کارشناس" icon="edit_note">
            <BusinessRequestActions
              requestId={request.id}
              specialists={specialists}
              slot="note"
            />
          </FormSection>
        </div>

        <div className="flex flex-col gap-lg lg:sticky lg:top-24">
          <FormSection title="اقدام سریع" icon="bolt">
            <div className="flex flex-col gap-sm">
              {/*
                Calling and e-mailing need no backend — they hand off to the
                operator's own phone and mail client. Assignment does, so it
                stays a form control rather than a link that goes nowhere.
              */}
              {[
                { label: 'تماس با فرد رابط', icon: 'call', href: `tel:${request.phone}` },
                {
                  label: 'ارسال ایمیل',
                  icon: 'mail',
                  href: `mailto:${request.email}?subject=${encodeURIComponent(
                    `درخواست سازمانی ${request.code}`,
                  )}`,
                },
              ].map((action) => (
                <a
                  key={action.label}
                  href={action.href}
                  className="flex items-center gap-sm rounded-lg border border-outline-variant px-md py-sm text-label-md font-medium text-on-surface transition-colors hover:bg-surface-container-low hover:text-primary"
                >
                  <Icon name={action.icon} size={20} />
                  {action.label}
                </a>
              ))}

              <BusinessRequestActions
                requestId={request.id}
                specialists={specialists}
                slot="aside"
              />
            </div>
          </FormSection>
        </div>
      </div>
    </AdminPage>
  );
}
