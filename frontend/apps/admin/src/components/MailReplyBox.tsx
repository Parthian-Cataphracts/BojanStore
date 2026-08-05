'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Textarea } from '@bojan/ui';
import { postJson } from '@/lib/submit';

/**
 * Answers a customer's email.
 *
 * The recipient, subject and threading headers are fixed by the conversation
 * rather than typed: an operator answering a support mail is not composing a
 * new one, and a free recipient field on this screen would be a way to send
 * mail from the shop's address to anywhere.
 */
export function MailReplyBox({
  to,
  subject,
  replyFolder,
  replyUid,
}: {
  to: string;
  subject: string;
  replyFolder?: string | null;
  replyUid?: number | null;
}) {
  const router = useRouter();
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const body = String(new FormData(form).get('body') ?? '').trim();
    if (!body) return;

    setSending(true);
    setError(null);
    try {
      await postJson('/api/admin/mail-reply', {
        to: [to],
        cc: [],
        // "Re:" once. The server normalises any run of prefixes when it
        // threads, so a subject that already carries one does not grow another
        // every time the exchange goes round.
        subject: subject.trim().toLowerCase().startsWith('re:') ? subject : `Re: ${subject}`,
        body,
        replyToFolder: replyFolder ?? null,
        inReplyToUid: replyUid ?? null,
      });

      setSent(true);
      form.reset();

      // The reply is filed into Sent, so it becomes part of the thread — the
      // screen has to re-read it to show what was just sent.
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال پاسخ انجام نشد.');
    } finally {
      setSending(false);
    }
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <div className="flex items-center gap-sm">
        <Icon name="reply" size={20} className="text-primary" />
        <h2 className="text-body-lg font-semibold text-primary">پاسخ</h2>
      </div>

      <p className="text-caption text-on-surface-variant">
        پاسخ از نشانی پشتیبانی فروشگاه و در همین گفتگو برای{' '}
        <span className="latin-inline">{to}</span> ارسال می‌شود.
      </p>

      <form onSubmit={submit} className="flex flex-col gap-md">
        <Textarea name="body" label="متن پاسخ" rows={6} required maxLength={20000} />

        <div className="flex flex-wrap items-center gap-md">
          <Button type="submit" loading={sending}>
            ارسال پاسخ
          </Button>

          {sent && (
            <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
              <Icon name="check_circle" size={16} />
              پاسخ ارسال شد.
            </span>
          )}
          {error && (
            <span role="alert" className="flex items-center gap-xs text-caption text-error">
              <Icon name="error" size={16} />
              {error}
            </span>
          )}
        </div>
      </form>
    </Card>
  );
}
