'use client';

import { useEffect, useRef, useState } from 'react';
import { usePathname } from 'next/navigation';
import { IconButton, Sheet, cn, formatDateTime } from '@bojan/ui';
import { isBareRoute } from '@/lib/chrome';

interface ChatMessage {
  id: string;
  fromSupport: boolean;
  body: string;
  sentAtUtc: string;
}

const POLL_MS = 4000;

/**
 * Floating live-chat launcher, present on every storefront page.
 *
 * The conversation is not keyed by a session — a visitor can chat before
 * signing in, the same way the contact form on screen 47 accepts an anonymous
 * sender. It is keyed by an id this component never sees: the server issues it
 * on the first message and keeps it in an http-only cookie, so a script on the
 * page cannot read whose conversation this is. While open it polls `/api/chat`
 * every few seconds; there is no push channel behind this, so "live" means
 * "catches up within one poll interval", not a socket.
 */
export function ChatWidget() {
  const bare = isBareRoute(usePathname());
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    let cancelled = false;

    async function poll() {
      try {
        const response = await fetch('/api/chat', { cache: 'no-store' });
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
  }, [open]);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight });
  }, [messages]);

  async function handleSend() {
    const text = draft.trim();
    if (!text || sending) return;

    setSending(true);
    setDraft('');
    // Optimistic: the next poll reconciles with the server's copy.
    setMessages((current) => [
      ...current,
      { id: `pending-${Date.now()}`, fromSupport: false, body: text, sentAtUtc: new Date().toISOString() },
    ]);

    try {
      await fetch('/api/chat/messages', {
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
      {/*
        Sits on top of whatever owns the bottom edge, never inside it.

        It used to be pinned `env(safe-area-inset-bottom) + 16px` from the
        bottom at `z-40`, which put it squarely inside the tab bar's 87px and
        one layer behind it — on a phone the launcher was simply not there.
        `.floating-above-bottom` rests it on the tab bar plus anything a page
        has stacked there, so it clears the bar on a phone and falls back to
        the same 16px from the edge at `lg`, where there is no bar to clear.
        `z-50` matches the bar rather than sitting under it; the two no longer
        overlap, but a control that hides behind furniture is worth being
        explicit about.
      */}
      <IconButton
        icon="chat"
        label="گفتگوی آنلاین با پشتیبانی"
        variant="surface"
        onClick={() => setOpen(true)}
        className={cn(
          'fixed end-4 z-50 h-14 w-14 bg-primary text-on-primary shadow-soft transition-[bottom] duration-200 hover:bg-primary/90 lg:end-8',
          // Sign-in and its siblings render no tab bar, so there is nothing to
          // clear there and the bar's reserved height would leave the button
          // hovering over blank page.
          bare ? 'floating-above-edge' : 'floating-above-bottom',
        )}
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
