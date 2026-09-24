import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

export type MailboxView = components["schemas"]["MailboxView"];
export type MailLetterView = components["schemas"]["MailLetterView"];
export type AnnouncementView = components["schemas"]["AnnouncementView"];
export type MailRewardView = components["schemas"]["MailRewardView"];
export type ClaimView = components["schemas"]["ClaimView"];

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

/** Нагорода могла впасти будь-куди: gems — гаманець, ресурси — село, предмети — інвентар. */
function useClaim<TInput>(playerId: string, path: (input: TInput) => string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: TInput) => api<ClaimView>(path(input), { method: "POST", idempotent: true }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["mail", playerId] });
      invalidatePlayer(queryClient, playerId, ["wallet", "village", "inventory", "heroes"]);
    },
  });
}

export function useClaimLetter(playerId: string) {
  return useClaim<string>(playerId, (letterId) => `/api/mail/${playerId}/letters/${letterId}/claim`);
}

export function useClaimAll(playerId: string) {
  return useClaim<void>(playerId, () => `/api/mail/${playerId}/claim-all`);
}

export function useMarkLetterRead(playerId: string) {
  return useMarkRead(playerId, "letters");
}

export function useMarkAnnouncementRead(playerId: string) {
  return useMarkRead(playerId, "announcements");
}
