import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Card, Icon, formatDateTime } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { ChatReplyBox } from '@/components/ChatReplyBox';
import { getChatConversation } from '@/lib/api/live-chat';

export const metadata: Metadata = { title: 'گفتگوی آنلاین' };

export default async function LiveChatConversationPage({
  params,
}: {
  params: Promise<{ visitorId: string }>;
}) {
  const { visitorId } = await params;
  const messages = await getChatConversation(visitorId);
  if (messages.length === 0) notFound();

  return (
    <AdminPage
      title="گفتگوی آنلاین"
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'پشتیبانی', href: '/support' },
        { label: 'گفتگوی آنلاین', href: '/support/live-chat' },
        { label: visitorId.slice(0, 8) },
      ]}
    >
      <section className="flex flex-col gap-md">
        {messages.map((message) => (
          <div key={message.id} className={message.fromSupport ? 'flex justify-start' : 'flex justify-end'}>
            <Card
              className={`flex max-w-[min(90%,42rem)] flex-col gap-sm p-lg ${
                message.fromSupport ? 'bg-soft-mint/40' : ''
              }`}
            >
              <span className="flex items-center gap-xs text-label-md font-semibold text-primary">
                <Icon name={message.fromSupport ? 'support_agent' : 'person'} size={18} />
                {message.fromSupport ? 'پشتیبانی بوژان' : 'بازدیدکننده'}
              </span>
              <p className="text-body-md leading-loose text-on-surface-variant">{message.body}</p>
              <span className="tabular text-caption text-outline">{formatDateTime(message.sentAtUtc)}</span>
            </Card>
          </div>
        ))}
      </section>

      <ChatReplyBox visitorId={visitorId} />
    </AdminPage>
  );
}
