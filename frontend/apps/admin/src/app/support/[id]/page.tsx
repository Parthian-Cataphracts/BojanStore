import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Badge, Card, Icon, formatDateTime, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { SupportReplyBox } from '@/components/SupportReplyBox';
import { getCannedReplies, getSupportThread } from '@/lib/api/support';
import { threadStatusMeta } from '@/lib/status';
import type { SupportThread } from '@/lib/types';

export const metadata: Metadata = { title: 'جزئیات گفتگوی پشتیبانی' };

/** Screen 131 — Support conversation. */
export default async function SupportThreadPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const [thread, cannedReplies] = await Promise.all([getSupportThread(id), getCannedReplies()]);
  if (!thread) notFound();

  const status = threadStatusMeta[thread.status as SupportThread['status']];

  return (
    <AdminPage
      title="جزئیات گفتگوی پشتیبانی"
      breadcrumbs={[{ label: 'پشتیبانی', href: '/support' }, { label: thread.subject }]}
    >
      <Card className="flex flex-wrap items-start justify-between gap-md p-lg">
        <div className="flex min-w-0 flex-col gap-xs">
          <h3 className="text-body-lg font-semibold text-primary">{thread.subject}</h3>
          <span className="tabular text-caption text-on-surface-variant">
            {thread.customer} · {toPersianDigits(thread.messages.length)} پیام · آخرین بروزرسانی{' '}
            {formatDateTime(thread.updatedAt)}
          </span>
        </div>

        <div className="flex shrink-0 items-center gap-sm">
          {(thread.priority as SupportThread['priority']) === 'high' && (
            <Badge tone="error">اولویت بالا</Badge>
          )}
          <Badge tone={status.tone}>{status.label}</Badge>
        </div>
      </Card>

      <section className="flex flex-col gap-md">
        {thread.messages.map((message) => (
          <div
            key={message.id}
            className={message.fromSupport ? 'flex justify-start' : 'flex justify-end'}
          >
            <Card
              className={`flex max-w-[min(90%,42rem)] flex-col gap-sm p-lg ${
                message.fromSupport ? 'bg-soft-mint/40' : ''
              }`}
            >
              <span className="flex items-center gap-xs text-label-md font-semibold text-primary">
                <Icon name={message.fromSupport ? 'support_agent' : 'person'} size={18} />
                {message.fromSupport ? 'پشتیبانی بوژان' : thread.customer}
              </span>
              <p className="text-body-md leading-loose text-on-surface-variant">{message.body}</p>
              <span className="tabular text-caption text-outline">
                {formatDateTime(message.sentAt)}
              </span>
            </Card>
          </div>
        ))}
      </section>

      <SupportReplyBox threadId={thread.id} cannedReplies={cannedReplies} />
    </AdminPage>
  );
}
