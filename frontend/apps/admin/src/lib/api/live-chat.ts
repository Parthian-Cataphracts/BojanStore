import { api, useMockData } from './client';
import type { LiveChatConversationDto, LiveChatMessageDto } from './types';

/** Screen (new) live chat — the storefront widget's conversations. No mock fixture: an empty console tells an operator nothing is wired, which is true in mock mode. */
export async function getChatConversations(): Promise<LiveChatConversationDto[]> {
  if (useMockData) return [];
  return api.get<LiveChatConversationDto[]>('/chat/conversations', { auth: true });
}

export async function getChatConversation(visitorId: string): Promise<LiveChatMessageDto[]> {
  if (useMockData) return [];
  try {
    return await api.get<LiveChatMessageDto[]>(`/chat/conversations/${visitorId}`, { auth: true });
  } catch {
    return [];
  }
}
