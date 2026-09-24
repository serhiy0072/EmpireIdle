import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";

export type MailboxView = components["schemas"]["MailboxView"];
export type MailLetterView = components["schemas"]["MailLetterView"];
export type AnnouncementView = components["schemas"]["AnnouncementView"];

/** Скринька з актуальним станом листів; нове підсвічує подія MailReceived. */
export function useMailbox(playerId: string): UseQueryResult<MailboxView> {
  return useQuery({
    queryKey: queryKeys.mailbox(playerId),
    queryFn: () => api<MailboxView>(`/api/mail/${playerId}`),
  });
}

/** Лічильник для бейджа в меню — легкий запит, а не вся скринька. */
export function useMailUnread(playerId: string): UseQueryResult<number> {
  return useQuery({
    queryKey: queryKeys.mailUnread(playerId),
    queryFn: () => api<number>(`/api/mail/${playerId}/unread`),
    enabled: playerId !== "",
  });
}

function useMarkRead(playerId: string, kind: "letters" | "announcements") {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/mail/${playerId}/${kind}/${id}/read`, { method: "POST", idempotent: true }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["mail", playerId] }),
  });
}

export function useMarkLetterRead(playerId: string) {
  return useMarkRead(playerId, "letters");
}

export function useMarkAnnouncementRead(playerId: string) {
  return useMarkRead(playerId, "announcements");
}
