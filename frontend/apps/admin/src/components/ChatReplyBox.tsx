'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Card, Icon, Select, Textarea } from '@bojan/ui';
import type { CannedReplyDto } from '@/lib/api/types';
import { postJson } from '@/lib/submit';

/**
 * Reply composer for one live-chat conversation.
 *
 * The canned replies were on the ticket composer and not on this one, and this
 * is the composer where they matter most: a live chat is answered while somebody
 * is waiting, which is the whole reason a shop writes stock answers in the first
 * place. Screen 132 saved them, screen 131 offered them, and the chat screen —
 * the fastest conversation in the panel — made an operator retype every one.
 */
export function ChatReplyBox({
  visitorId,
  cannedReplies = [],
}: {
  visitorId: string;
  cannedReplies?: CannedReplyDto[];
}) {
  const router = useRouter();
  const [body, setBody] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();

    if (body.trim().length === 0) {
      setError('متن پاسخ را بنویسید.');
      return;
    }

    setError(null);
    setSending(true);
    try {
      await postJson('/api/admin/chat-reply', { visitorId, body: body.trim() });
      setBody('');
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ارسال پاسخ انجام نشد.');
    } finally {
      setSending(false);
    }
  }

  return (
    <Card className="flex flex-col gap-md p-lg">
      <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
        <Icon name="reply" size={22} />
        پاسخ
      </h3>

      <form onSubmit={submit} noValidate className="flex flex-col gap-md">
        {/* Only when the shop has written some — an empty picker is a control
            that teaches an operator the feature does not work. */}
        {cannedReplies.length > 0 && (
          <Select
            label="پاسخ آماده"
            defaultValue=""
            onChange={(event) => {
              const reply = cannedReplies.find((item) => item.id === event.target.value);
              if (reply) {
                setBody(reply.body);
                setError(null);
              }
            }}
          >
            <option value="">انتخاب از پاسخ‌های آماده…</option>
            {cannedReplies.map((reply) => (
              <option key={reply.id} value={reply.id}>
                {reply.title}
              </option>
            ))}
          </Select>
        )}

        <Textarea
          label="متن پاسخ"
          rows={3}
          value={body}
          onChange={(event) => {
            setBody(event.target.value);
            setError(null);
          }}
          {...(error ? { error } : null)}
        />

        <div className="flex flex-wrap gap-md">
          <Button type="submit" loading={sending} icon="send" className="px-xl">
            ارسال پاسخ
          </Button>
        </div>
      </form>
    </Card>
  );
}
