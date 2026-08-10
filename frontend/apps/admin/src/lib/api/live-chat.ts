import { api, useMockData } from './client';
import type { LiveChatConversationDto, LiveChatMessageDto } from './types';

/** Screen (new) live chat — the storefront widget's conversations. No mock fixture: an empty console tells an operator nothing is wired, which is true in mock mode. */
export async function getChatConversations(): Promise<LiveChatConversationDto[]> {
  if (useMockData) return [];
  return api.get<LiveChatConversationDto[]>('/chat/conversations', { auth: true });
}

/**
 * One conversation, oldest message first.
 *
 * Errors are deliberately not swallowed. They used to be — every failure
 * became an empty list, and the page turns an empty list into `notFound()`,
 * so a backend that was answering 500 told the operator the conversation did
 * not exist. A thread with nothing in it already comes back as an empty 200,
 * which is the only case that should read as "not found".
 */
export async function getChatConversation(visitorId: string): Promise<LiveChatMessageDto[]> {
  if (useMockData) return [];
  return api.get<LiveChatMessageDto[]>(`/chat/conversations/${visitorId}`, { auth: true });
}
