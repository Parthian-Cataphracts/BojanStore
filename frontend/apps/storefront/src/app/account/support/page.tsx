import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Card, EmptyState, Icon, buttonClasses, formatDateTime } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getTickets } from '@/lib/api/activity';
import { ticketStatusMeta } from '@/lib/mock/activity';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'پیام‌های پشتیبانی',
  robots: { index: false },
};

/** Screen 54 — Support messages. */
export default async function SupportPage() {
  const tickets = await getTickets();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="پیام‌های پشتیبانی" backHref={routes.account} />

      <Card className="flex items-start gap-md p-lg">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="support_agent" size={24} />
        </span>
        <p className="text-body-md leading-loose text-on-surface-variant">
          پیام‌ها و پاسخ‌های پشتیبانی بوژان را اینجا دنبال کنید. تیم ما در اسرع وقت پاسخگوی شما
          خواهد بود.
        </p>
      </Card>

      {tickets.length > 0 ? (
        <ul className="flex flex-col gap-md">
          {tickets.map((ticket) => {
            const status = ticketStatusMeta[ticket.status];
            return (
              <li key={ticket.id}>
                <Card className="flex flex-col gap-sm p-lg">
                  <div className="flex flex-wrap items-start justify-between gap-sm">
                    <h2 className="min-w-0 text-label-md font-label-md text-primary">
                      {ticket.subject}
                    </h2>
                    <Badge tone={status.tone}>
                      <Icon name={status.icon} size={16} />
                      {status.label}
                    </Badge>
                  </div>

                  <p className="line-clamp-2 text-body-md leading-relaxed text-on-surface-variant">
                    {ticket.lastMessageFromSupport && (
                      <span className="font-label-md text-primary">پشتیبانی: </span>
                    )}
                    {ticket.lastMessage}
                  </p>

                  <div className="flex items-center justify-between gap-md pt-sm">
                    <span className="tabular text-caption text-outline">
                      {formatDateTime(ticket.updatedAt)}
                    </span>
                    <Link
                      href={`${routes.support}/${ticket.id}`}
                      className="inline-flex items-center gap-xs text-label-md font-label-md text-secondary transition-colors hover:text-primary"
                    >
                      مشاهده گفتگو
                      <Icon name="chevron_left" size={18} />
                    </Link>
                  </div>
                </Card>
              </li>
            );
          })}
        </ul>
      ) : (
        <EmptyState
          icon="forum"
          title="پیامی ثبت نشده است"
          description="اگر سوالی دارید، از طریق فرم تماس با ما پیام بگذارید."
        />
      )}

      <Link
        href={routes.contact}
        className={buttonClasses({ size: 'lg', className: 'gap-sm self-start px-xl' })}
      >
        <Icon name="add_comment" size={20} />
        ثبت پیام جدید
      </Link>
    </Container>
  );
}
