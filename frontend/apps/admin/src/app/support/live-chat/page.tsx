import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, buttonClasses, formatDateTime, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { AutoRefresh } from '@/components/AutoRefresh';
import { DataTable, type Column } from '@/components/DataTable';
import { getChatConversations } from '@/lib/api/live-chat';
import type { LiveChatConversationDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'گفتگوی آنلاین' };

const columns: Column<LiveChatConversationDto>[] = [
  {
    key: 'visitor',
    header: 'بازدیدکننده',
    cell: (row) => <span className="tabular text-caption">{row.visitorId.slice(0, 8)}…</span>,
  },
  {
    key: 'last',
    header: 'آخرین پیام',
    cell: (row) => (
      <span className="line-clamp-1 max-w-sm text-body-md">
        {row.lastFromSupport ? 'شما: ' : ''}
        {row.lastMessage}
      </span>
    ),
  },
  {
    key: 'unread',
    header: 'خوانده‌نشده',
    cell: (row) =>
      row.unreadCount > 0 ? (
        <Badge tone="warning">{toPersianDigits(row.unreadCount)}</Badge>
      ) : (
        <span className="text-caption text-on-surface-variant">—</span>
      ),
  },
  {
    key: 'updated',
    header: 'آخرین فعالیت',
    cell: (row) => <span className="tabular">{formatDateTime(row.lastMessageAt)}</span>,
  },
];

/** Live-chat conversations from the storefront widget. */
export default async function LiveChatPage() {
  const conversations = await getChatConversations();

  return (
    <AdminPage
      title="گفتگوی آنلاین"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'پشتیبانی', href: '/support' }, { label: 'گفتگوی آنلاین' }]}
    >
      {/* A shopper writing in should appear in the queue without the operator
          reloading to find out. Slower than the open thread — this is a list
          being watched, not a conversation being held. */}
      <AutoRefresh seconds={15} />

      <DataTable
        columns={columns}
        rows={conversations}
        rowKey={(row) => row.visitorId}
        emptyTitle="گفتگویی وجود ندارد"
        emptyDescription="پیامی از ویجت گفتگوی فروشگاه دریافت نشده است."
        emptyIcon="chat"
        actions={(row) => (
          <Link
            href={`/support/live-chat/${row.visitorId}`}
            className={buttonClasses({ variant: 'outline', size: 'sm' })}
          >
            مشاهده گفتگو
          </Link>
        )}
      />
    </AdminPage>
  );
}
