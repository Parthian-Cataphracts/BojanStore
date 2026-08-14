'use client';

import { useMemo, useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Badge, Button, Card, FormStatus, Icon, Input, Select, Textarea, toPersianDigits } from '@bojan/ui';
import { postJson } from '@/lib/submit';
import type { AdminAccountDto, AdminNotificationDto } from '@/lib/api/types';

/**
 * «ارسال اعلان» — compose on one side, everything already sent on the other.
 *
 * The screen used to be a broadcast form alone: pick an audience group, write a
 * message, send. Two things were missing and both are what an operator reaches
 * for first. There was no way to write to *one* person from here — that lived
 * on the customer's own record — and no way to see what had already gone out,
 * so "did we already tell them?" had no answer anywhere in the panel.
 *
 * Search on both halves, because both lists grow without bound: a shop with
 * four thousand customers cannot be given a dropdown, and a year of messages
 * cannot be read by scrolling.
 */

/** SMS parts are 70 characters in Persian (UCS-2), not 160. */
const SMS_PART = 70;

const channels = [
  { id: 'in-app', label: 'اعلان درون‌برنامه‌ای' },
  { id: 'sms', label: 'پیامک' },
  { id: 'email', label: 'ایمیل' },
  { id: 'push', label: 'اعلان مرورگر' },
];

function formatSent(iso: string): string {
  if (!iso) return '';
  try {
    return new Intl.DateTimeFormat('fa-IR', {
      calendar: 'persian',
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return '';
  }
}

export function NotificationCenter({
  accounts,
  sent,
  pushEnabled,
  initialCustomerId,
}: {
  /** Everyone a message can be addressed to, for the recipient picker. */
  accounts: AdminAccountDto[];
  sent: AdminNotificationDto[];
  pushEnabled: boolean;
  /** Set when the operator arrived from a customer's record. */
  initialCustomerId?: string;
}) {
  const router = useRouter();

  const [target, setTarget] = useState<string>(initialCustomerId ?? 'all');
  const [channel, setChannel] = useState('in-app');
  const [recipientSearch, setRecipientSearch] = useState('');
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [link, setLink] = useState('');
  const [sending, setSending] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [note, setNote] = useState<{ ok: boolean; text: string } | null>(null);

  const [listSearch, setListSearch] = useState('');
  const [removing, setRemoving] = useState<string | null>(null);

  // Only shoppers can receive a customer notification: an operator's account is
  // a way into the panel, not an inbox the storefront renders.
  const customers = useMemo(
    () => accounts.filter((account) => account.role === 'customer'),
    [accounts],
  );

  const matches = useMemo(() => {
    const needle = recipientSearch.trim().toLowerCase();
    if (!needle) return customers.slice(0, 50);
    return customers
      .filter((account) =>
        [account.name, account.phone, account.email, account.code]
          .filter(Boolean)
          .some((field) => String(field).toLowerCase().includes(needle)),
      )
      .slice(0, 50);
  }, [customers, recipientSearch]);

  const visible = useMemo(() => {
    const needle = listSearch.trim().toLowerCase();
    if (!needle) return sent;
    return sent.filter((item) =>
      [item.title, item.body, item.recipient]
        .filter(Boolean)
        .some((field) => String(field).toLowerCase().includes(needle)),
    );
  }, [sent, listSearch]);

  const broadcast = target === 'all';
  const parts = channel === 'sms' ? Math.max(1, Math.ceil(body.length / SMS_PART)) : 0;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!title.trim()) {
      setNote({ ok: false, text: 'عنوان پیام را وارد کنید.' });
      return;
    }

    if (link.trim() && !link.trim().startsWith('/')) {
      setNote({ ok: false, text: 'لینک باید یک مسیر داخلی سایت باشد و با / شروع شود.' });
      return;
    }

    // A broadcast cannot be recalled, so the second press is the real one. A
    // message to one person can be deleted from the list beside this form, so
    // it does not need the ceremony.
    if (broadcast && !confirming) {
      setConfirming(true);
      return;
    }

    setSending(true);
    setNote(null);
    try {
      if (broadcast) {
        await postJson('/api/admin/notifications', {
          channel,
          audience: 'all',
          title: title.trim(),
          body: body.trim(),
        });
      } else {
        await postJson('/api/admin/customer-notify', {
          customerId: target,
          title: title.trim(),
          body: body.trim(),
          link: link.trim() || null,
        });
      }

      setTitle('');
      setBody('');
      setLink('');
      setConfirming(false);
      setNote({
        ok: true,
        text: broadcast ? 'پیام عمومی در صف ارسال قرار گرفت.' : 'پیام برای این کاربر ارسال شد.',
      });
      // The sent list is rendered on the server, so it is refetched rather than
      // patched here — one answer about what has gone out, not two.
      router.refresh();
    } catch (cause) {
      setConfirming(false);
      setNote({ ok: false, text: cause instanceof Error ? cause.message : 'ارسال پیام انجام نشد.' });
    } finally {
      setSending(false);
    }
  }

  async function remove(item: AdminNotificationDto) {
    setRemoving(item.id);
    try {
      await postJson('/api/admin/notification-delete', { kind: item.kind, id: item.id });
      router.refresh();
    } catch (cause) {
      setNote({ ok: false, text: cause instanceof Error ? cause.message : 'حذف پیام انجام نشد.' });
    } finally {
      setRemoving(null);
    }
  }

  return (
    <div className="grid gap-lg lg:grid-cols-[380px_1fr]">
      <Card className="h-fit p-lg">
        <h2 className="mb-md text-card-title text-primary">ارسال پیام جدید</h2>

        <form onSubmit={submit} className="flex flex-col gap-md" noValidate>
          <label className="flex flex-col gap-2xs">
            <span className="text-label-md text-on-surface-variant">گیرنده</span>
            <Select value={target} onChange={(event) => setTarget(event.target.value)}>
              <option value="all">همه کاربران (عمومی)</option>
              {matches.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.name || account.phone} {account.code ? `(${account.code})` : ''}
                </option>
              ))}
            </Select>
          </label>

          {/*
            The picker above is capped at fifty rows. A shop with thousands of
            customers has a dropdown nobody can find anybody in, so this is how
            the rest are reached.
          */}
          <label className="flex flex-col gap-2xs">
            <span className="text-label-md text-on-surface-variant">جستجوی کاربر</span>
            <Input
              value={recipientSearch}
              onChange={(event) => setRecipientSearch(event.target.value)}
              placeholder="نام، موبایل، ایمیل یا شناسه..."
            />
            <span className="text-caption text-on-surface-variant">
              {toPersianDigits(matches.length)} کاربر در فهرست بالا
            </span>
          </label>

          {broadcast && (
            <label className="flex flex-col gap-2xs">
              <span className="text-label-md text-on-surface-variant">کانال ارسال</span>
              <Select value={channel} onChange={(event) => setChannel(event.target.value)}>
                {channels.map((option) => (
                  <option
                    key={option.id}
                    value={option.id}
                    // Browser notifications only deliver once the owner has
                    // generated the key pair, so the option says so rather than
                    // failing after the send.
                    disabled={option.id === 'push' && !pushEnabled}
                  >
                    {option.label}
                    {option.id === 'push' && !pushEnabled ? ' — تنظیم نشده' : ''}
                  </option>
                ))}
              </Select>
            </label>
          )}

          <label className="flex flex-col gap-2xs">
            <span className="text-label-md text-on-surface-variant">عنوان</span>
            <Input value={title} onChange={(event) => setTitle(event.target.value)} />
          </label>

          <label className="flex flex-col gap-2xs">
            <span className="text-label-md text-on-surface-variant">متن پیام</span>
            <Textarea rows={4} value={body} onChange={(event) => setBody(event.target.value)} />
            {parts > 0 && (
              <span className="tabular text-caption text-on-surface-variant">
                {toPersianDigits(body.length)} کاراکتر — {toPersianDigits(parts)} پیامک
              </span>
            )}
          </label>

          {!broadcast && (
            <label className="flex flex-col gap-2xs">
              <span className="text-label-md text-on-surface-variant">لینک (اختیاری)</span>
              <Input
                value={link}
                onChange={(event) => setLink(event.target.value)}
                dir="ltr"
                placeholder="/account/orders"
                className="text-left"
              />
            </label>
          )}

          <Button type="submit" disabled={sending || !title.trim()}>
            {sending
              ? 'در حال ارسال...'
              : confirming
                ? 'مطمئنم، برای همه بفرست'
                : broadcast
                  ? 'ارسال به همه'
                  : 'ارسال پیام'}
          </Button>

          {confirming && (
            <p className="text-body-sm leading-relaxed text-warning">
              این پیام به همه‌ی مشتریان می‌رود و قابل بازگشت نیست.
            </p>
          )}

          {note && <FormStatus ok={note.ok ? note.text : null} error={note.ok ? null : note.text} />}
        </form>
      </Card>

      <Card className="h-fit p-lg">
        <div className="mb-md flex flex-wrap items-center justify-between gap-sm">
          <h2 className="text-card-title text-primary">پیام‌های ارسال‌شده</h2>
          <span className="text-caption text-on-surface-variant">
            {toPersianDigits(visible.length)} پیام
          </span>
        </div>

        <Input
          value={listSearch}
          onChange={(event) => setListSearch(event.target.value)}
          placeholder="جستجو در عنوان، متن یا گیرنده..."
          className="mb-md"
        />

        {visible.length === 0 ? (
          <p className="py-xl text-center text-body-md text-on-surface-variant">
            {sent.length === 0 ? 'هنوز پیامی ارسال نشده است.' : 'پیامی با این جستجو پیدا نشد.'}
          </p>
        ) : (
          <ul className="flex flex-col gap-sm">
            {visible.map((item) => (
              <li
                key={`${item.kind}-${item.id}`}
                className="flex items-start justify-between gap-md rounded-lg border border-outline-variant bg-surface p-md"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-xs">
                    <span className="text-label-lg font-semibold text-primary">{item.title}</span>
                    <Badge tone={item.kind === 'broadcast' ? 'teal' : 'neutral'}>
                      {item.recipient}
                    </Badge>
                  </div>
                  {item.body && (
                    <p className="mt-2xs text-body-sm leading-relaxed text-on-surface-variant">
                      {item.body}
                    </p>
                  )}
                  <p className="tabular mt-2xs text-caption text-on-surface-variant">
                    {formatSent(item.sentAt)}
                  </p>
                </div>

                <Button
                  variant="ghost"
                  onClick={() => remove(item)}
                  disabled={removing === item.id}
                  className="shrink-0 gap-2xs text-error"
                >
                  <Icon name="delete" size={18} />
                  {removing === item.id ? 'حذف...' : 'حذف'}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
