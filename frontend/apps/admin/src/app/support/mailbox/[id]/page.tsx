import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Card, Icon, buttonClasses, formatDateTime } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { MailBody } from '@/components/MailBody';
import { MailReplyBox } from '@/components/MailReplyBox';
import { getMailConversation, isMailboxError } from '@/lib/api/mailbox';

export const metadata: Metadata = { title: 'گفتگوی ایمیلی' };

/**
 * One email thread, whole.
 *
 * The customer's messages and our replies in one column, oldest first, each
 * body rendered in its own sandboxed frame. Opening this marks the customer's
 * messages read — the same thing opening a conversation in a mail client does.
 */
export default async function MailThreadPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const conversation = await getMailConversation(id);

  if (isMailboxError(conversation)) {
    return (
      <AdminPage
        title="گفتگوی ایمیلی"
        breadcrumbs={[
          { label: 'پشتیبانی', href: '/support' },
          { label: 'صندوق پستی', href: '/support/mailbox' },
          { label: 'گفتگو' },
        ]}
      >
        <Card className="flex flex-col items-start gap-md p-lg">
          <p className="flex items-center gap-xs text-body-md text-error">
            <Icon name="error" size={20} />
            {conversation.error}
          </p>
          <Link
            href="/support/mailbox"
            className={buttonClasses({ variant: 'outline', size: 'sm' })}
          >
            بازگشت به صندوق
          </Link>
        </Card>
      </AdminPage>
    );
  }

  const party = conversation.party.address;

  return (
    <AdminPage
      title={conversation.subject}
      description={conversation.party.name || party}
      breadcrumbs={[
        { label: 'پشتیبانی', href: '/support' },
        { label: 'صندوق پستی', href: '/support/mailbox' },
        { label: 'گفتگو' },
      ]}
    >
      <ol className="flex flex-col gap-md">
        {conversation.messages.map((message) => (
          <li key={`${message.folder}-${message.uid}`}>
            <Card
              className={
                message.fromCustomer
                  ? 'flex flex-col gap-sm p-lg'
                  : // Our own replies are tinted, so the two sides of the
                    // exchange are distinguishable at a glance without reading
                    // the addresses.
                    'flex flex-col gap-sm border-s-4 border-s-primary bg-surface-container-low p-lg'
              }
            >
              <div className="flex flex-wrap items-center justify-between gap-sm">
                <div className="flex items-center gap-sm">
                  <Badge tone={message.fromCustomer ? 'teal' : 'neutral'}>
                    {message.fromCustomer ? 'مشتری' : 'پاسخ ما'}
                  </Badge>
                  <span className="latin-inline text-caption text-on-surface-variant">
                    {message.from.name ? `${message.from.name} · ` : ''}
                    {message.from.address}
                  </span>
                </div>

                <span className="tabular text-caption text-on-surface-variant">
                  {formatDateTime(message.date)}
                </span>
              </div>

              <MailBody
                html={message.htmlBody}
                text={message.textBody}
                hadRemoteContent={message.hadRemoteContent}
              />

              {message.attachments.length > 0 && (
                <div className="flex flex-wrap items-center gap-sm border-t border-outline-variant/40 pt-sm">
                  <span className="text-caption text-on-surface-variant">پیوست‌ها:</span>
                  {message.attachments.map((attachment) => (
                    <a
                      key={attachment.index}
                      // Always a download, never rendered — the file and its
                      // name come from whoever sent the mail.
                      href={`/api/admin/mail-attachment?folder=${encodeURIComponent(message.folder)}&uid=${message.uid}&index=${attachment.index}`}
                      className={buttonClasses({
                        variant: 'outline',
                        size: 'sm',
                        className: 'gap-xs',
                      })}
                    >
                      <Icon name="attach_file" size={16} />
                      <span className="latin-inline">{attachment.fileName}</span>
                    </a>
                  ))}
                </div>
              )}
            </Card>
          </li>
        ))}
      </ol>

      {party && (
        <MailReplyBox
          to={party}
          subject={conversation.subject}
          replyFolder={conversation.replyFolder}
          replyUid={conversation.replyUid}
        />
      )}
    </AdminPage>
  );
}
