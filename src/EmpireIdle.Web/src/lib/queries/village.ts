import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { VillageResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";

/** Той самий ключ інвалідує подія BuildingCollected — збір у сусідній вкладці оновить цей екран. */
export function useVillage(playerId: string): UseQueryResult<VillageResponse> {
  return useQuery({
    queryKey: queryKeys.village(playerId),
    queryFn: () => api<VillageResponse>(`/api/village/${playerId}`),
  });
}

function useBuildingAction(playerId: string, action: "collect" | "upgrade" | "speedup") {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (buildingId: string) =>
      api<void>(`/api/village/${playerId}/buildings/${buildingId}/${action}`, { method: "POST" }),
    // Сервер порахує сам: свій підрахунок розійшовся б із виробітком по секундах
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.village(playerId) }),
  });
}

export function useCollectBuilding(playerId: string) {
  return useBuildingAction(playerId, "collect");
}

export function useUpgradeBuilding(playerId: string) {
  return useBuildingAction(playerId, "upgrade");
}

export function useSpeedUpBuilding(playerId: string) {
  return useBuildingAction(playerId, "speedup");
}
