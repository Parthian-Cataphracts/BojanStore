import { ApiError, api, useMockData } from './client';
import type {
  MailConversationDetailDto,
  MailConversationPageDto,
  MailboxSettingsDto,
} from './types';

/**
 * The support mailbox.
 *
 * No mock fixtures. Every other admin module falls back to sample data so the
 * screens can be worked on without a backend, but inventing an inbox would mean
 * an operator in mock mode reading messages from customers who do not exist and
 * possibly answering them. An empty inbox that says the mailbox is not
 * configured is the truth in that mode.
 */

const NOT_CONFIGURED = 'صندوق پستی در این حالت در دسترس نیست.';

export interface ListMailQuery {
  q?: string;
  unread?: boolean;
  page?: number;
  pageSize?: number;
}

/** What a failed mailbox call looks like to the screens. */
export interface MailboxError {
  error: string;
}

export type MailboxResult<T> = T | MailboxError;

export function isMailboxError<T>(result: MailboxResult<T>): result is MailboxError {
  return typeof result === 'object' && result !== null && 'error' in result;
}

/**
 * Pulls the API's message out of a failed response.
 *
 * The mailbox endpoints answer 502 with a problem document whose `detail` is a
 * sentence written for the operator — "the password was not accepted", "no SMTP
 * host is set". Losing it and showing "something went wrong" would throw away
 * the only part of the failure that tells them what to do.
 */
function messageFrom(cause: unknown): string {
  /*
    The problem document first, which is the whole point of this function and
    was the one thing it did not do. `ApiError.message` is this client's own
    generic line — «درخواست /support/mailbox/conversations با خطای 502 مواجه
    شد» — and the API's sentence is in `ApiError.body.detail`, where
    `problemMessage` reads it. So the screen was told exactly which door was
    shut and printed the number of the door instead: an operator whose mailbox
    is simply not set up read a 502 and reasonably concluded the shop was
    broken.
  */
  const detail = detailFrom(cause);
  if (detail) return detail;

  if (cause instanceof ApiError) return 'ارتباط با صندوق پستی برقرار نشد.';
  if (cause instanceof Error && cause.message) return cause.message;
  return 'ارتباط با صندوق پستی برقرار نشد.';
}

/** The `detail` the mailbox endpoints write their operator-facing sentence into. */
function detailFrom(cause: unknown): string | null {
  if (!(cause instanceof ApiError)) return null;

  const body = cause.body;
  if (typeof body !== 'object' || body === null) return null;

  const { detail } = body as { detail?: unknown };
  return typeof detail === 'string' && detail.length > 0 ? detail : null;
}

export async function getMailConversations(
  query: ListMailQuery = {},
): Promise<MailboxResult<MailConversationPageDto>> {
  if (useMockData) return { error: NOT_CONFIGURED };

  try {
    return await api.get<MailConversationPageDto>('/support/mailbox/conversations', {
      query: {
        q: query.q,
        unread: query.unread ? 'true' : undefined,
        page: query.page ?? 1,
        pageSize: query.pageSize ?? 25,
      },
      auth: true,
    });
  } catch (cause) {
    return { error: messageFrom(cause) };
  }
}

export async function getMailConversation(
  id: string,
): Promise<MailboxResult<MailConversationDetailDto>> {
  if (useMockData) return { error: NOT_CONFIGURED };

  try {
    return await api.get<MailConversationDetailDto>(
      `/support/mailbox/conversations/${encodeURIComponent(id)}`,
      { auth: true },
    );
  } catch (cause) {
    return { error: messageFrom(cause) };
  }
}

export async function getMailboxSettings(): Promise<MailboxSettingsDto> {
  if (useMockData) {
    return {
      enabled: false,
      imapHost: '',
      imapPort: 993,
      imapUseSsl: true,
      smtpHost: '',
      smtpPort: 587,
      smtpUseSsl: true,
      username: '',
      hasPassword: false,
      address: '',
      displayName: 'پشتیبانی بوژان',
    };
  }

  return api.get<MailboxSettingsDto>('/support/mailbox/settings', { auth: true });
}

/** Unread inbox count for the nav badge; zero when the mailbox is off or unreachable. */
export async function getMailUnreadCount(): Promise<number> {
  if (useMockData) return 0;

  try {
    const result = await api.get<{ count: number }>('/support/mailbox/unread-count', {
      auth: true,
    });
    return result.count ?? 0;
  } catch {
    return 0;
  }
}
