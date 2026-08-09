'use client';

import { Fragment, useCallback, useEffect, useRef, useState } from 'react';
import { usePathname } from 'next/navigation';
import { Icon, cn, formatDate, toPersianDigits } from '@bojan/ui';
import { isBareRoute } from '@/lib/chrome';

interface ChatMessage {
  id: string;
  fromSupport: boolean;
  body: string;
  sentAtUtc: string;
  /** Read by the other side — what the delivery ticks and the badge are drawn from. */
  read: boolean;
}

/**
 * Two rates, because the panel being open changes what a poll is for.
 *
 * Open, it is the conversation itself and has to feel live. Closed, it only
 * feeds the launcher's badge, so it runs slowly enough to be free on a page
 * left in a background tab.
 */
const POLL_OPEN_MS = 2500;
const POLL_CLOSED_MS = 12000;

/** Optimistic rows carry a local id; the server's are GUIDs. */
const PENDING_PREFIX = 'pending-';

/** Any page can open the panel: `window.dispatchEvent(new Event(OPEN_CHAT_EVENT))`. */
export const OPEN_CHAT_EVENT = 'bojan:open-chat';

function timeOf(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return toPersianDigits(
    new Intl.DateTimeFormat('fa-IR-u-nu-latn', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(date),
  );
}

/** Groups messages into days without formatting them first. */
function dayKey(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
}

/** «امروز» / «دیروز» / a Jalali date, for the separator between two days of messages. */
function dayLabel(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';

  const midnight = (value: Date) =>
    new Date(value.getFullYear(), value.getMonth(), value.getDate()).getTime();
  const days = Math.round((midnight(new Date()) - midnight(date)) / 86_400_000);

  if (days <= 0) return 'امروز';
  if (days === 1) return 'دیروز';
  return formatDate(date);
}

/**
 * Delivery state, in the shape every messaging app has taught people to read:
 * a clock while it is still going, one check once the server has it, two once
 * support has opened the thread.
 */
function Ticks({ state }: { state: 'sending' | 'sent' | 'read' }) {
  const label =
    state === 'sending' ? 'در حال ارسال' : state === 'read' ? 'خوانده شد' : 'ارسال شد';

  return (
    <>
      <Icon
        name={state === 'sending' ? 'schedule' : state === 'read' ? 'done_all' : 'check'}
        size={14}
        className={cn('shrink-0', state === 'read' ? 'opacity-100' : 'opacity-70')}
      />
      <span className="sr-only">{label}</span>
    </>
  );
}

/**
 * The floating support conversation.
 *
 * A panel anchored above its own launcher rather than a bottom sheet: a sheet
 * takes the whole width and covers the page it was opened from, which is the
 * wrong trade for a conversation you keep half an eye on while you carry on
 * shopping.
 *
 * The conversation is not keyed by a session — a visitor can chat before
 * signing in, the same way the contact form on screen 47 accepts an anonymous
 * sender. It is keyed by an id this component never sees: the server issues it
 * on the first message and keeps it in a signed, http-only cookie, so a script
 * on the page cannot read whose conversation this is.
 *
 * "Live" means "catches up within one poll", not a socket — there is no push
 * channel behind this.
 */
export function ChatWidget() {
  const bare = isBareRoute(usePathname());

  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  /** The newest support reply, previewed above the launcher while the panel is shut. */
  const [notice, setNotice] = useState<{ body: string; time: string } | null>(null);

  const listRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  /** A poll landing mid-send would drop the optimistic row before the server echoed it. */
  const sendingRef = useRef(false);
  /** The last reply already shown as a toast, so the same one never pops twice. */
  const noticedRef = useRef<string | null>(null);

  const unread = messages.filter((message) => message.fromSupport && !message.read).length;

  const load = useCallback(async (): Promise<ChatMessage[] | null> => {
    try {
      const response = await fetch('/api/chat', { cache: 'no-store' });
      if (!response.ok) return null;
      return (await response.json()) as ChatMessage[];
    } catch {
      // A missed poll just tries again next tick; the last state stays on screen.
      return null;
    }
  }, []);

  // Any page can ask for the panel — the support card on a product page, a
  // "talk to us" link in an empty state.
  useEffect(() => {
    const openChat = () => {
      setOpen(true);
      setNotice(null);
    };
    window.addEventListener(OPEN_CHAT_EVENT, openChat);
    return () => window.removeEventListener(OPEN_CHAT_EVENT, openChat);
  }, []);

  // Closed: poll slowly, purely to keep the badge and the toast current. The
  // fetch deliberately does not mark anything read — that is what kept the
  // badge from ever appearing before this was split in two.
  useEffect(() => {
    if (bare || open) return;

    let alive = true;
    const tick = async () => {
      const next = await load();
      if (!alive || !next) return;

      setMessages(next);

      const latest = next.filter((message) => message.fromSupport && !message.read).pop();
      if (latest && latest.id !== noticedRef.current) {
        noticedRef.current = latest.id;
        setNotice({ body: latest.body, time: timeOf(latest.sentAtUtc) });
      }
    };

    tick();
    const timer = window.setInterval(tick, POLL_CLOSED_MS);
    return () => {
      alive = false;
      window.clearInterval(timer);
    };
  }, [bare, open, load]);

  // Open: poll quickly, and tell the server the replies have been seen — the
  // shopper is looking straight at them.
  useEffect(() => {
    if (bare || !open) return;

    setNotice(null);
    let alive = true;

    const tick = async () => {
      if (sendingRef.current) return;
      const next = await load();
      if (!alive || sendingRef.current || !next) return;

      setMessages(next);

      const latestSupport = next.filter((message) => message.fromSupport).pop();
      if (latestSupport) noticedRef.current = latestSupport.id;

      if (next.some((message) => message.fromSupport && !message.read)) {
        fetch('/api/chat/read', { method: 'POST' }).catch(() => {});
      }
    };

    tick();
    const timer = window.setInterval(tick, POLL_OPEN_MS);
    return () => {
      alive = false;
      window.clearInterval(timer);
    };
  }, [bare, open, load]);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight });
  }, [messages.length, open]);

  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  // Escape closes the panel, the way every other overlay in the shop does.
  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open]);

  if (bare) return null;

  async function send() {
    const body = draft.trim();
    if (!body || sending) return;

    const optimistic: ChatMessage = {
      id: `${PENDING_PREFIX}${Date.now()}`,
      fromSupport: false,
      body,
      sentAtUtc: new Date().toISOString(),
      read: false,
    };

    setMessages((current) => [...current, optimistic]);
    setDraft('');
    setSending(true);
    sendingRef.current = true;

    try {
      const response = await fetch('/api/chat/messages', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ body }),
      });
      if (!response.ok) throw new Error('rejected');

      sendingRef.current = false;
      const next = await load();
      if (next) setMessages(next);
    } catch {
      // Put the text back rather than losing it, and take the row that never
      // landed back off the screen.
      setDraft((current) => current || body);
      setMessages((current) => current.filter((message) => message.id !== optimistic.id));
    } finally {
      setSending(false);
      sendingRef.current = false;
    }
  }

  const last = messages[messages.length - 1];
  /*
   * Support has opened the thread on the shopper's latest message and not
   * answered yet — the one moment where "typing" is a fair thing to imply.
   */
  const awaitingReply =
    open && !!last && !last.fromSupport && !last.id.startsWith(PENDING_PREFIX) && last.read;

  return (
    /*
     * One stack for the panel, the toast and the launcher, so all three clear
     * the tab bar by the same amount and none of them can drift on top of it.
     * `.floating-above-bottom` rests the stack on the bar plus whatever a page
     * has put on top of it; at `lg`, where there is no bar, it collapses to the
     * same 16px from the edge that `.floating-above-edge` gives the sign-in
     * screens, which render no bar at all.
     */
    <div
      className={cn(
        'fixed end-4 z-50 flex flex-col items-end gap-sm lg:end-8',
        bare ? 'floating-above-edge' : 'floating-above-bottom',
      )}
    >
      {open && (
        <div
          role="dialog"
          aria-label="گفتگو با پشتیبانی بوژان"
          /*
           * Sized against the space actually left above the bar rather than a
           * flat `vh`: on a phone in landscape a fixed 28rem panel would run
           * off the top of the screen, and `100vh` measures the viewport with
           * the address bar hidden, which it is not while you are reading. The
           * 8rem subtracted is the launcher plus the gap above and below it.
           */
          className="flex h-[28rem] max-h-[calc(100dvh_-_var(--bottom-stack)_-_8rem)] w-[21rem] max-w-[calc(100vw_-_2rem)] flex-col overflow-hidden rounded-xl border border-paper-border bg-warm-paper shadow-soft"
        >
          <header className="flex items-center gap-sm border-b border-paper-border bg-gradient-to-l from-soft-mint via-soft-mint/40 to-transparent px-md py-sm">
            <span className="relative grid h-10 w-10 shrink-0 place-items-center rounded-full bg-primary text-on-primary">
              <Icon name="support_agent" size={22} />
              <span className="absolute -bottom-px -end-px h-3 w-3 rounded-full border-2 border-warm-paper bg-emerald-500" />
            </span>

            <div className="min-w-0 flex-1">
              <p className="truncate text-label-md font-label-md text-primary">پشتیبانی بوژان</p>
              <p className="flex items-center gap-xs text-caption text-on-surface-variant">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                آنلاین · معمولاً چند دقیقه
              </p>
            </div>

            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label="بستن گفتگو"
              className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary"
            >
              <Icon name="close" size={20} />
            </button>
          </header>

          <div ref={listRef} className="flex-1 space-y-sm overflow-y-auto px-md py-md">
            {messages.length === 0 ? (
              <p className="m-auto max-w-[16rem] py-lg text-center text-body-md leading-relaxed text-on-surface-variant">
                سلام! هر سوالی درباره سفارش یا محصولات دارید همین‌جا بنویسید؛ تیم پشتیبانی بوژان
                پاسخ می‌دهد.
              </p>
            ) : (
              messages.map((message, index) => {
                const previous = messages[index - 1];
                const newDay = !previous || dayKey(previous.sentAtUtc) !== dayKey(message.sentAtUtc);
                const pending = message.id.startsWith(PENDING_PREFIX);

                return (
                  <Fragment key={message.id}>
                    {newDay && (
                      <div className="flex justify-center py-xs">
                        <span className="rounded-full bg-surface-container px-sm py-xs text-caption text-on-surface-variant">
                          {dayLabel(message.sentAtUtc)}
                        </span>
                      </div>
                    )}

                    {/*
                      The shopper's own messages sit on the start side and
                      support's on the end — right and left respectively, once
                      the page's RTL is applied. Logical properties throughout,
                      so the whole thread mirrors itself if this is ever read
                      in an LTR locale.
                    */}
                    <div className={cn('flex', message.fromSupport ? 'justify-end' : 'justify-start')}>
                      <div
                        className={cn(
                          'max-w-[85%] rounded-xl px-md py-sm text-body-md leading-relaxed',
                          // The corner nearest the speaker is squared off, the
                          // way a tail would attach.
                          message.fromSupport
                            ? 'rounded-ee bg-surface-container-high text-on-surface'
                            : 'rounded-es bg-primary text-on-primary',
                        )}
                      >
                        <p className="whitespace-pre-wrap break-words">{message.body}</p>
                        <p
                          className={cn(
                            'mt-xs flex items-center justify-end gap-xs text-caption',
                            message.fromSupport ? 'text-on-surface-variant' : 'text-on-primary/70',
                          )}
                        >
                          {!message.fromSupport && (
                            <Ticks state={pending ? 'sending' : message.read ? 'read' : 'sent'} />
                          )}
                          <span className="tabular">{timeOf(message.sentAtUtc)}</span>
                        </p>
                      </div>
                    </div>
                  </Fragment>
                );
              })
            )}

            {awaitingReply && (
              <div className="flex justify-end">
                <div
                  className="flex items-center gap-1 rounded-xl rounded-ee bg-surface-container-high px-md py-sm"
                  role="status"
                >
                  <span className="sr-only">پشتیبانی در حال نوشتن است</span>
                  {[0, 200, 400].map((delay) => (
                    <span
                      key={delay}
                      aria-hidden
                      className="h-1.5 w-1.5 rounded-full bg-on-surface-variant motion-safe:animate-pulse"
                      style={{ animationDelay: `${delay}ms`, animationDuration: '1s' }}
                    />
                  ))}
                </div>
              </div>
            )}
          </div>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              send();
            }}
            className="flex items-end gap-sm border-t border-paper-border p-sm"
          >
            <textarea
              ref={inputRef}
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={(event) => {
                // Enter sends, Shift+Enter breaks the line — the convention
                // every chat box shares, and the reason this is not an <input>.
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault();
                  send();
                }
              }}
              rows={1}
              maxLength={4000}
              placeholder="پیام خود را بنویسید…"
              aria-label="متن پیام"
              className="max-h-24 min-h-[2.75rem] flex-1 resize-none rounded-lg border border-outline-variant bg-surface-container-lowest px-md py-sm text-body-md text-on-surface outline-none transition-colors placeholder:text-outline focus:border-primary focus:ring-2 focus:ring-primary/30"
            />
            <button
              type="submit"
              disabled={sending || !draft.trim()}
              aria-label="ارسال پیام"
              className="grid h-11 w-11 shrink-0 place-items-center rounded-lg bg-primary text-on-primary transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <Icon name="send" size={20} />
            </button>
          </form>
        </div>
      )}

      {!open && notice && (
        <div className="relative flex w-[19rem] max-w-[calc(100vw-2rem)] gap-sm rounded-xl border border-paper-border bg-warm-paper p-sm shadow-soft">
          {/*
            The card is the button. A <button> wrapping this would nest the
            dismiss control inside a control, which no browser lets you click.
          */}
          <button
            type="button"
            onClick={() => {
              setOpen(true);
              setNotice(null);
            }}
            className="absolute inset-0 rounded-xl"
            aria-label="باز کردن گفتگو با پشتیبانی"
          />

          <span className="relative grid h-9 w-9 shrink-0 place-items-center rounded-full bg-primary text-on-primary">
            <Icon name="support_agent" size={18} />
          </span>

          <div className="pointer-events-none min-w-0 flex-1">
            <div className="flex items-center justify-between gap-xs">
              <b className="truncate text-caption font-label-md text-primary">پشتیبانی بوژان</b>
              <span className="tabular shrink-0 text-caption text-on-surface-variant">
                {notice.time}
              </span>
            </div>
            <p className="mt-xs line-clamp-2 text-caption leading-relaxed text-on-surface-variant">
              {notice.body}
            </p>
          </div>

          <button
            type="button"
            onClick={() => setNotice(null)}
            aria-label="بستن اعلان"
            className="relative grid h-6 w-6 shrink-0 place-items-center self-start rounded-full text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary"
          >
            <Icon name="close" size={14} />
          </button>
        </div>
      )}

      <button
        type="button"
        onClick={() => {
          setOpen((current) => !current);
          setNotice(null);
        }}
        aria-label={open ? 'بستن گفتگوی آنلاین' : 'گفتگوی آنلاین با پشتیبانی'}
        aria-expanded={open}
        className="relative grid h-14 w-14 shrink-0 place-items-center rounded-full bg-primary text-on-primary shadow-soft transition-transform hover:bg-primary-container active:scale-95"
      >
        {!open && unread > 0 && (
          <span
            aria-hidden
            className="absolute inset-0 rounded-full border-2 border-primary/40 motion-safe:animate-ping"
          />
        )}

        <Icon name={open ? 'close' : 'support_agent'} size={26} className="relative" />

        {!open && unread > 0 && (
          <span className="absolute -end-1 -top-1 grid h-6 min-w-6 place-items-center rounded-full border-2 border-warm-paper bg-secondary-container px-1 text-caption font-label-md text-on-primary">
            {unread > 9 ? '۹+' : toPersianDigits(unread)}
          </span>
        )}
      </button>
    </div>
  );
}
