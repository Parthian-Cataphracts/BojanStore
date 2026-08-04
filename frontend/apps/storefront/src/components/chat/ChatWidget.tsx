'use client';

import { useEffect, useRef, useState } from 'react';
import { IconButton, Sheet, cn, formatDateTime } from '@bojan/ui';

interface ChatMessage {
  id: string;
  fromSupport: boolean;
  body: string;
  sentAtUtc: string;
}

const VISITOR_ID_KEY = 'bojan-chat-visitor-id';
const POLL_MS = 4000;

function getVisitorId(): string {
  const existing = window.localStorage.getItem(VISITOR_ID_KEY);
  if (existing) return existing;

  const id = crypto.randomUUID();
  window.localStorage.setItem(VISITOR_ID_KEY, id);
  return id;
}

/**
 * Floating live-chat launcher, present on every storefront page.
 *
 * The conversation is keyed by an opaque id kept in `localStorage` rather
 * than a session — a visitor can chat before signing in, the same way the
 * contact form on screen 47 accepts an anonymous sender. While open it polls
 * `/api/chat/[visitorId]` every few seconds; there is no push channel behind
 * this, so "live" means "catches up within one poll interval", not a socket.
 */
export function ChatWidget() {
  const [open, setOpen] = useState(false);
  const [visitorId, setVisitorId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setVisitorId(getVisitorId());
  }, []);

  useEffect(() => {
    if (!open || !visitorId) return;

    let cancelled = false;

    async function poll() {
      try {
        const response = await fetch(`/api/chat/${visitorId}`, { cache: 'no-store' });
        if (!response.ok || cancelled) return;
        const data = (await response.json()) as ChatMessage[];
        if (!cancelled) setMessages(data);
      } catch {
        // A missed poll just tries again next tick.
      }
    }

    poll();
    const interval = window.setInterval(poll, POLL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(interval);
    };
  }, [open, visitorId]);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight });
  }, [messages]);

  async function handleSend() {
    const text = draft.trim();
    if (!text || !visitorId || sending) return;

    setSending(true);
    setDraft('');
    // Optimistic: the next poll reconciles with the server's copy.
    setMessages((current) => [
      ...current,
      { id: `pending-${Date.now()}`, fromSupport: false, body: text, sentAtUtc: new Date().toISOString() },
    ]);

    try {
      await fetch(`/api/chat/${visitorId}/messages`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ body: text }),
      });
    } finally {
      setSending(false);
    }
  }

  return (
    <>
      <IconButton
        icon="chat"
        label="گفتگوی آنلاین با پشتیبانی"
        variant="surface"
        onClick={() => setOpen(true)}
        className="fixed bottom-[calc(env(safe-area-inset-bottom)+16px)] end-4 z-40 h-14 w-14 bg-primary text-on-primary shadow-soft hover:bg-primary/90 lg:end-8"
      />

      <Sheet open={open} onClose={() => setOpen(false)} title="گفتگو با پشتیبانی بوژان" placement="bottom" className="lg:max-w-md lg:ms-auto lg:me-4 lg:mb-4 lg:rounded-xl">
        <div ref={listRef} className="flex max-h-[50vh] min-h-[240px] flex-col gap-sm overflow-y-auto">
          {messages.length === 0 && (
            <p className="m-auto max-w-xs text-center text-body-md text-on-surface-variant">
              سوالی دارید؟ همین‌جا بنویسید، تیم پشتیبانی بوژان پاسخ می‌دهد.
            </p>
          )}

          {messages.map((message) => (
            <div
              key={message.id}
              className={cn('flex flex-col gap-2xs', message.fromSupport ? 'items-start' : 'items-end')}
            >
              <div
                className={cn(
                  'max-w-[80%] rounded-xl px-md py-sm text-body-md leading-relaxed',
                  message.fromSupport
                    ? 'bg-surface-container-high text-on-surface'
                    : 'bg-primary text-on-primary',
                )}
              >
                {message.body}
              </div>
              <span className="px-xs text-caption text-on-surface-variant">
                {formatDateTime(message.sentAtUtc)}
              </span>
            </div>
          ))}
        </div>

        <form
          className="mt-md flex items-end gap-sm"
          onSubmit={(event) => {
            event.preventDefault();
            handleSend();
          }}
        >
          <textarea
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                handleSend();
              }
            }}
            rows={1}
            placeholder="پیام خود را بنویسید…"
            className="max-h-28 flex-1 resize-none rounded-lg border border-outline-variant bg-surface px-md py-sm text-body-md text-on-surface outline-none focus:border-primary"
          />
          <IconButton
            icon="send"
            label="ارسال پیام"
            type="submit"
            disabled={!draft.trim() || sending}
            className="bg-primary text-on-primary disabled:opacity-50"
          />
        </form>
      </Sheet>
    </>
  );
}
