import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { QuestState, QuestView, QuestWindow, ServerQuestResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

/** Значення enum-ів з контракту: числа на дроті, імена — тут. */
export const QUEST_STATE = {
  inProgress: 1,
  completed: 2,
  claimed: 3,
} as const satisfies Record<string, QuestState>;

export const QUEST_WINDOW = {
  chain: 1,
  daily: 2,
  event: 3,
} as const satisfies Record<string, QuestWindow>;

/** Щоденні квести обнуляються о 00:00 UTC — так само їх скидає джоб на сервері. */
export function nextDailyResetAt(now: number): string {
  const next = new Date(now);
  next.setUTCHours(24, 0, 0, 0);

  return next.toISOString();
}

export function useQuests(playerId: string): UseQueryResult<QuestView[]> {
  return useQuery({
    queryKey: queryKeys.quests(playerId),
    queryFn: () => api<QuestView[]>(`/api/quests/${playerId}`),
  });
}

export function useServerQuests(playerId: string): UseQueryResult<ServerQuestResponse[]> {
  return useQuery({
    queryKey: queryKeys.serverQuests(playerId),
    queryFn: () => api<ServerQuestResponse[]>(`/api/server-quests/${playerId}`),
  });
}

/**
 * Нагорода може бути чим завгодно: gems, ресурси, герой чи уламки, предмет.
 * Інвалідуємо все, що вона здатна зачепити — точний тип відомий лише серверу.
 */
export function useClaimQuest(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (questKey: string) =>
      api<void>(`/api/quests/${playerId}/${questKey}/claim`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["quests", "wallet", "village", "heroes"]),
  });
}
