'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Card, Icon, Textarea } from '@bojan/ui';
import { postJson } from '@/lib/submit';

/** Reply composer for one live-chat conversation. */
export function ChatReplyBox({ visitorId }: { visitorId: string }) {
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
