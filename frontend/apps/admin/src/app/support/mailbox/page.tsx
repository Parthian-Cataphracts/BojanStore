import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Card, EmptyState, Icon, buttonClasses, formatDateTime, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { FilterBar } from '@/components/FilterBar';
import { getMailConversations, isMailboxError } from '@/lib/api/mailbox';

export const metadata: Metadata = { title: 'صندوق پستی پشتیبانی' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 25;

/**
 * The support inbox, as conversations.
 *
 * Grouped rather than listed message by message, because a support exchange is
 * a back-and-forth: a flat list interleaves four unrelated topics from one
 * customer with the replies to each, and an operator has to reconstruct which
 * answer went with which question.
 */
export default async function MailboxPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const unread = first(params.unread) === 'true';
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  const result = await getMailConversations({ q: query, unread, page, pageSize: PAGE_SIZE });

  return (
    <AdminPage
      title="صندوق پستی پشتیبانی"
      description="ایمیل‌هایی که مشتریان به نشانی پشتیبانی فروشگاه فرستاده‌اند."
      breadcrumbs={[{ label: 'پشتیبانی', href: '/support' }, { label: 'صندوق پستی' }]}
      actions={
        <Link
          href="/support/mailbox/settings"
          className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
        >
          <Icon name="settings" size={18} />
          تنظیمات صندوق
        </Link>
      }
    >
      {isMailboxError(result) ? (
        // The API's own sentence, not a generic message: it is the difference
        // between "the mailbox is not set up" and "the password was rejected",
        // and only one of those is something the operator can fix from here.
        <Card className="p-lg">
          <EmptyState
            icon="mail"
            title="صندوق پستی در دسترس نیست"
            description={result.error}
            action={
              <Link
                href="/support/mailbox/settings"
                className={buttonClasses({ variant: 'primary', size: 'sm' })}
              >
                رفتن به تنظیمات صندوق
              </Link>
            }
          />
        </Card>
      ) : (
        <>
          <Suspense fallback={null}>
            <FilterBar
              searchPlaceholder="جستجوی موضوع، نام یا نشانی فرستنده..."
              filters={[
                {
                  param: 'unread',
                  label: 'وضعیت',
                  options: [{ value: 'true', label: 'فقط خوانده‌نشده' }],
                },
              ]}
            />
          </Suspense>

          {result.items.length === 0 ? (
            <Card className="p-lg">
              <EmptyState
                icon="mark_email_read"
                title={query || unread ? 'گفتگویی یافت نشد' : 'صندوق خالی است'}
                description={
                  query || unread
                    ? 'با این فیلترها ایمیلی پیدا نشد.'
                    : 'هنوز ایمیلی به نشانی پشتیبانی نرسیده است.'
                }
              />
            </Card>
          ) : (
            <ul className="flex flex-col gap-sm">
              {result.items.map((conversation) => (
                <li key={conversation.id}>
                  <Link
                    href={`/support/mailbox/${encodeURIComponent(conversation.id)}`}
                    className="flex flex-col gap-xs rounded-lg border border-outline-variant bg-surface-container-lowest p-md transition-colors hover:bg-surface-container-low"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-sm">
                      <span
                        className={
                          conversation.unread > 0
                            ? 'text-body-md font-semibold text-on-surface'
                            : 'text-body-md text-on-surface'
                        }
                      >
                        {conversation.subject}
                      </span>

                      <span className="tabular shrink-0 text-caption text-on-surface-variant">
                        {formatDateTime(conversation.lastDate)}
                      </span>
                    </div>

                    <div className="flex flex-wrap items-center gap-sm text-caption text-on-surface-variant">
                      <span>{conversation.party.name || conversation.party.address}</span>

                      {conversation.count > 1 && (
                        <span className="tabular">{toPersianDigits(conversation.count)} پیام</span>
                      )}

                      {conversation.hasAttachments && <Icon name="attach_file" size={14} />}

                      {conversation.unread > 0 && (
                        <Badge tone="teal">{toPersianDigits(conversation.unread)} خوانده‌نشده</Badge>
                      )}

                      {/* The one thing an operator scanning this list needs:
                          which of these is waiting on them. */}
                      {!conversation.lastFromCustomer && <Badge tone="neutral">پاسخ داده شده</Badge>}
                    </div>

                    <p className="line-clamp-2 text-caption text-outline">{conversation.preview}</p>
                  </Link>
                </li>
              ))}
            </ul>
          )}

          {result.total > PAGE_SIZE && (
            <div className="flex items-center justify-center gap-md">
              {page > 1 && (
                <Link
                  href={{ pathname: '/support/mailbox', query: { ...params, page: page - 1 } }}
                  className={buttonClasses({ variant: 'outline', size: 'sm' })}
                >
                  قبلی
                </Link>
              )}

              <span className="tabular text-caption text-on-surface-variant">
                صفحه {toPersianDigits(page)} از {toPersianDigits(Math.ceil(result.total / PAGE_SIZE))}
              </span>

              {page * PAGE_SIZE < result.total && (
                <Link
                  href={{ pathname: '/support/mailbox', query: { ...params, page: page + 1 } }}
                  className={buttonClasses({ variant: 'outline', size: 'sm' })}
                >
                  بعدی
                </Link>
              )}
            </div>
          )}
        </>
      )}
    </AdminPage>
  );
}
