import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { GarrisonResponse, HealPaymentMethod } from "../apiTypes";
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
    mutationFn: (request: { unitType: string; level: number; count: number }) =>
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

export function useLevelUpUnits(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: { unitType: string; fromLevel: number; toLevel: number; count: number }) =>
      api<void>(`/api/garrisons/${playerId}/units/levelup`, {
        method: "POST",
        body: request,
        idempotent: true,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.garrison(playerId) }),
  });
}

export function useSpeedUpLevelUp(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (orderId: string) =>
      api<void>(`/api/garrisons/${playerId}/levelup/${orderId}/speedup`, { method: "POST", idempotent: true }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.garrison(playerId) }),
  });
}

/** Ключі — рядки "unitType@level" (wire-формат UnitStackKey). */
export function useHealWounded(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: { units: Record<string, number>; payment: HealPaymentMethod }) =>
      api<void>(`/api/garrisons/${playerId}/units/heal`, {
        method: "POST",
        body: request,
        idempotent: true,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.garrison(playerId) }),
  });
}

/** Ключі — рядки "unitType@level" (wire-формат UnitStackKey). */
export function useRecoverUnits(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: { units: Record<string, number> }) =>
      api<void>(`/api/garrisons/${playerId}/units/recover`, {
        method: "POST",
        body: request,
        idempotent: true,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.garrison(playerId) }),
  });
}
