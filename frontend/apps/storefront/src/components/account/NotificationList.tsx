'use client';

import Link from 'next/link';
import { useState } from 'react';
import { Card, EmptyState, Icon, cn, formatDateTime } from '@bojan/ui';
import type { Notification, NotificationKind } from '@/lib/api/types';
import { postJson } from '@/lib/api/submit';
import { notificationKindMeta } from '@/lib/mock/activity';

const filters: { value: NotificationKind | 'all'; label: string }[] = [
  { value: 'all', label: 'همه' },
  { value: 'order', label: notificationKindMeta.order.label },
  { value: 'offer', label: notificationKindMeta.offer.label },
  { value: 'account', label: notificationKindMeta.account.label },
  { value: 'stock', label: notificationKindMeta.stock.label },
];

/** Screen 53 — Notifications, with the kind filter and mark-all-read. */
export function NotificationList({ initial }: { initial: Notification[] }) {
  const [items, setItems] = useState(initial);
  const [kind, setKind] = useState<NotificationKind | 'all'>('all');

  const visible = kind === 'all' ? items : items.filter((item) => item.kind === kind);
  const unread = items.filter((item) => !item.read).length;

  function markAllRead() {
    const ids = items.filter((item) => !item.read).map((item) => item.id);
    if (ids.length === 0) return;

    const previous = items;
    setItems((current) => current.map((item) => ({ ...item, read: true })));

    void postJson('/api/account/notifications-read', { ids }).catch(() => setItems(previous));
  }

  function markRead(id: string) {
    const previous = items;
    setItems((current) => current.map((item) => (item.id === id ? { ...item, read: true } : item)));

    void postJson('/api/account/notifications-read', { ids: [id] }).catch(() =>
      setItems(previous),
    );
  }

  return (
    <div className="flex flex-col gap-lg">
      <div className="flex items-center justify-between gap-md">
        <nav
          aria-label="فیلتر اعلان‌ها"
          className="hide-scrollbar -mx-margin-mobile flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
        >
          {filters.map((filter) => (
            <button
              key={filter.value}
              type="button"
              onClick={() => setKind(filter.value)}
              className={cn(
                'shrink-0 whitespace-nowrap rounded-full px-md py-sm text-label-md font-label-md transition-colors',
                kind === filter.value
                  ? 'bg-primary-fixed text-on-primary-fixed'
                  : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
              )}
            >
              {filter.label}
            </button>
          ))}
        </nav>

        {unread > 0 && (
          <button
            type="button"
            onClick={markAllRead}
            aria-label="خواندن همه اعلان‌ها"
            className="flex shrink-0 items-center gap-xs text-label-md font-label-md text-secondary transition-colors hover:text-primary"
          >
            <Icon name="done_all" size={20} />
            <span className="hidden sm:inline">همه را خوانده‌ام</span>
          </button>
        )}
      </div>

      {visible.length > 0 ? (
        <ul className="flex flex-col gap-md">
          {visible.map((item) => {
            const meta = notificationKindMeta[item.kind];
            const inner = (
              <Card
                className={cn(
                  'relative flex items-start gap-md p-lg transition-shadow hover:shadow-soft',
                  !item.read && 'border-primary/30',
                )}
              >
                {/* Unread marker, mint accent as drawn. */}
                {!item.read && (
                  <span
                    aria-label="خوانده‌نشده"
                    className="absolute end-lg top-lg h-2 w-2 rounded-full bg-soft-mint ring-2 ring-primary/40"
                  />
                )}

                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                  <Icon name={meta.icon} size={22} />
                </span>

                <div className="flex min-w-0 flex-1 flex-col gap-xs">
                  <h3
                    className={cn(
                      'text-body-md',
                      item.read ? 'text-on-surface' : 'font-label-md text-primary',
                    )}
                  >
                    {item.title}
                  </h3>
                  <p className="text-caption leading-loose text-on-surface-variant">{item.body}</p>
                  <span className="tabular text-caption text-outline">
                    {formatDateTime(item.createdAt)}
                  </span>
                </div>
              </Card>
            );

            return (
              <li key={item.id}>
                {item.href ? (
                  <Link href={item.href} onClick={() => markRead(item.id)} className="block">
                    {inner}
                  </Link>
                ) : (
                  inner
                )}
              </li>
            );
          })}
        </ul>
      ) : (
        <EmptyState
          icon="notifications_off"
          title="اعلانی در این دسته نیست"
          description="وقتی خبری باشد، همین‌جا به شما اطلاع می‌دهیم."
        />
      )}
    </div>
  );
}
