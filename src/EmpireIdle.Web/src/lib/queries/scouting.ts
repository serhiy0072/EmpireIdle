import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { ScoutReportResponse, SendScoutRequest } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

/** Останні звіти розвідки. Новий приходить подією ScoutReportReady — вона й інвалідує список. */
export function useScoutReports(playerId: string): UseQueryResult<ScoutReportResponse[]> {
  return useQuery({
    queryKey: queryKeys.scoutReports(playerId),
    queryFn: () => api<ScoutReportResponse[]>(`/api/marches/${playerId}/scout-reports`),
  });
}

/** Розвідники йдуть без героя й юнітів — гарнізон і герої не змінюються, лише список маршів. */
export function useSendScout(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: SendScoutRequest) =>
      api<string>(`/api/marches/${playerId}/scout`, { method: "POST", body: request, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["marches"]),
  });
}

/** Підписи результатів розвідки для гравця. */
export const SCOUT_OUTCOMES: Record<string, string> = {
  Success: "Розвідано",
  Blocked: "Село сховане від розвідки",
  TargetMoved: "Ціль переселилась — на місці порожньо",
  TargetGone: "Цілі вже немає",
};
