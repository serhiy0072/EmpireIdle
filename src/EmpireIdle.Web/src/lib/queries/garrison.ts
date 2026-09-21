import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { GarrisonResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";

export function useGarrison(playerId: string): UseQueryResult<GarrisonResponse> {
  return useQuery({
    queryKey: queryKeys.garrison(playerId),
    queryFn: () => api<GarrisonResponse>(`/api/garrisons/${playerId}`),
  });
}

export function useTrainUnits(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: { unitType: string; count: number }) =>
      api<void>(`/api/garrisons/${playerId}/units/train`, {
        method: "POST",
        body: request,
        idempotent: true,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.garrison(playerId) }),
  });
}

export function useSpeedUpTraining(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (orderId: string) =>
      api<void>(`/api/garrisons/${playerId}/training/${orderId}/speedup`, { method: "POST", idempotent: true }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.garrison(playerId) }),
  });
}
