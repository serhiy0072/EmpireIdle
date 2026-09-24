import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { CollectAllResponse, VillageResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";

/** Той самий ключ інвалідує подія BuildingCollected — збір у сусідній вкладці оновить цей екран. */
export function useVillage(playerId: string): UseQueryResult<VillageResponse> {
  return useQuery({
    queryKey: queryKeys.village(playerId),
    queryFn: () => api<VillageResponse>(`/api/village/${playerId}`),
    // Подія UpgradeCompleted може не дійти (обрив хабу) — дедлайн будівництва перепитуємо й самі
    // Так само момент самовідновлення: після нього темп і серія інші
    refetchInterval: refetchAtDue<VillageResponse>((village) =>
      village.buildings.flatMap((building) => [building.constructionCompletesAt, building.damagedUntil]),
    ),
  });
}

function useBuildingAction(
  playerId: string,
  action: "collect" | "upgrade" | "speedup",
  scopes: readonly PlayerScope[],
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (buildingId: string) =>
      api<void>(`/api/village/${playerId}/buildings/${buildingId}/${action}`, { method: "POST", idempotent: true }),
    // Сервер порахує сам: свій підрахунок розійшовся б із виробітком по секундах
    onSuccess: () => invalidatePlayer(queryClient, playerId, scopes),
  });
}

export function useCollectBuilding(playerId: string) {
  return useBuildingAction(playerId, "collect", ["village"]);
}

export function useUpgradeBuilding(playerId: string) {
  return useBuildingAction(playerId, "upgrade", ["village"]);
}

/** Прискорення платить gems з гаманця акаунта. */
export function useSpeedUpBuilding(playerId: string) {
  return useBuildingAction(playerId, "speedup", ["village", "wallet"]);
}

/**
 * Миттєвий ремонт усіх пошкоджених будівель за ресурси. Лише всі разом:
 * серію поразок обнуляє тільки повне відновлення (GDD §2.6).
 */
export function useRepairVillage(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api<void>(`/api/village/${playerId}/repair`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["village"]),
  });
}

/**
 * Зібрати все одразу: один запит замість кліку по кожній будівлі.
 * Повний склад не валить запит — він приходить у fullStorages.
 */
export function useCollectAll(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () =>
      api<CollectAllResponse>(`/api/village/${playerId}/collect-all`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["village"]),
  });
}
