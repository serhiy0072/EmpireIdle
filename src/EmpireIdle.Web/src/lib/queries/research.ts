import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { ResearchView } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";
import { refetchAtDue } from "./polling";

export function useResearch(playerId: string): UseQueryResult<ResearchView> {
  return useQuery({
    queryKey: queryKeys.research(playerId),
    queryFn: () => api<ResearchView>(`/api/research/${playerId}`),
    // Дослідження завершує сканер без події клієнту
    refetchInterval: refetchAtDue<ResearchView>((research) => (research.active == null ? [] : [research.active.completesAt])),
  });
}

/** Старт списує ресурси села. */
export function useStartResearch(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: { tier: number; node: string }) =>
      api<void>(`/api/research/${playerId}/start`, { method: "POST", body: request, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["research", "village"]),
  });
}

/** Прискорення платить gems і одразу завершує дослідження. */
export function useSpeedUpResearch(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api<void>(`/api/research/${playerId}/speedup`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["research", "wallet", "power"]),
  });
}
