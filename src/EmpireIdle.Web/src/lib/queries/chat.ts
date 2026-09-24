import { useMutation, useQuery, useQueryClient, type QueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { ChatMessageEvent } from "../realtime/events";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";

export type ChatMessageView = components["schemas"]["ChatMessageView"];
export type ChatConversationView = components["schemas"]["ChatConversationView"];
export type PlayerSettingsView = components["schemas"]["PlayerSettingsView"];

/** Канал у запитах — числом, як enum сервера; у view і подіях — рядком. */
export const CHAT_CHANNEL = { Server: 1, Clan: 2, Private: 3 } as const;
export type ChatChannelName = keyof typeof CHAT_CHANNEL;

export const HISTORY_SIZE = 50;

export function useChatHistory(
  playerId: string,
  channel: ChatChannelName,
  partnerId: string | null,
): UseQueryResult<ChatMessageView[]> {
  const params = new URLSearchParams({ take: String(HISTORY_SIZE) });
  if (partnerId !== null) params.set("partnerId", partnerId);

  return useQuery({
    queryKey: queryKeys.chatHistory(playerId, channel, partnerId),
    queryFn: () => api<ChatMessageView[]>(`/api/chat/${playerId}/${channel}?${params.toString()}`),
    // Нове приходить подією — перечитувати історію за таймером нема потреби
    staleTime: Infinity,
    enabled: channel !== "Private" || partnerId !== null,
  });
}

export function useChatConversations(playerId: string): UseQueryResult<ChatConversationView[]> {
  return useQuery({
    queryKey: queryKeys.chatConversations(playerId),
    queryFn: () => api<ChatConversationView[]>(`/api/chat/${playerId}/conversations`),
  });
}

/** Надсилання. Власне повідомлення з'явиться в історії подією — як і в усіх інших. */
export function useSendChat(playerId: string) {
  return useMutation({
    mutationFn: (input: { channel: ChatChannelName; recipientId: string | null; text: string }) =>
      api<{ messageId: string }>(`/api/chat/${playerId}/messages`, {
        method: "POST",
        idempotent: true,
        body: { channel: CHAT_CHANNEL[input.channel], recipientId: input.recipientId, text: input.text },
      }),
  });
}

export function usePlayerSettings(playerId: string): UseQueryResult<PlayerSettingsView> {
  return useQuery({
    queryKey: queryKeys.settings(playerId),
    queryFn: () => api<PlayerSettingsView>(`/api/player/${playerId}/settings`),
    staleTime: Infinity,
  });
}

/**
 * Зміна мови перечитує все, що від неї залежить: каталог (назви) і чат (переклади).
 */
export function useChangeLanguage(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (language: string) =>
      api<void>(`/api/player/${playerId}/language`, { method: "POST", idempotent: true, body: { language } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.settings(playerId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.catalog });
      void queryClient.invalidateQueries({ queryKey: ["chat", playerId] });
    },
  });
}

/**
 * Дописує подію в кеш історії того каналу, до якого вона належить.
 * Для приватного — розмова з тим, хто не я. Якщо історію ще не вантажили,
 * кеш не створюємо: при відкритті її прочитає запит.
 */
export function appendChatEvent(queryClient: QueryClient, playerId: string, language: string, event: ChatMessageEvent): void {
  const partnerId =
    event.channel === "Private" ? (event.senderId === playerId ? event.recipientId : event.senderId) : null;

  const view: ChatMessageView = {
    id: event.id,
    channel: event.channel,
    senderId: event.senderId,
    senderName: event.senderName,
    recipientId: event.recipientId,
    text: event.text,
    language: event.language,
    translatedText: event.language === language ? null : (event.translations[language] ?? null),
    sentAt: event.sentAt,
    isOwn: event.senderId === playerId,
  };

  queryClient.setQueryData<ChatMessageView[]>(queryKeys.chatHistory(playerId, event.channel, partnerId), (history) =>
    history === undefined || history.some((m) => m.id === view.id) ? history : [...history, view],
  );
}
