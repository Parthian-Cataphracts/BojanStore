'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Select, Textarea } from '@bojan/ui';
import type { CannedReplyDto } from '@/lib/api/types';
import { postJson } from '@/lib/submit';

/** Reply composer on screen 131, with canned replies from screen 132. */
export function SupportReplyBox({
  threadId,
  cannedReplies,
}: {
  threadId: string;
  cannedReplies: CannedReplyDto[];
}) {
  const [body, setBody] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();

    if (body.trim().length < 5) {
      setError('متن پاسخ را بنویسید.');
      return;
    }

    setError(null);
    setSending(true);
    try {
      await postJson('/api/admin/support-replies', { threadId, body: body.trim() });
      setBody('');
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
        <Select
          label="پاسخ آماده"
          defaultValue=""
          onChange={(event) => {
            const reply = cannedReplies.find((item) => item.id === event.target.value);
            if (reply) setBody(reply.body);
          }}
        >
          <option value="">انتخاب از پاسخ‌های آماده…</option>
          {cannedReplies.map((reply) => (
            <option key={reply.id} value={reply.id}>
              {reply.title}
            </option>
          ))}
        </Select>

        <Textarea
          label="متن پاسخ"
          rows={4}
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
