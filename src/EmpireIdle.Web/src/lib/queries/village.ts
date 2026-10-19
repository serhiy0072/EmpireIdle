import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { VillageResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";

/** Крок автозбору на сервері (GetVillageIdsDueAutoCollectQuery.Interval). */
const AUTO_COLLECT_MS = 60_000;

const atDue = refetchAtDue<VillageResponse>((village) =>
  village.buildings.flatMap((building) => [building.constructionCompletesAt, building.damagedUntil]),
);

/** Той самий ключ інвалідує подія BuildingCollected — збір у сусідній вкладці оновить цей екран. */
export function useVillage(playerId: string): UseQueryResult<VillageResponse> {
  return useQuery({
    queryKey: queryKeys.village(playerId),
    queryFn: () => api<VillageResponse>(`/api/village/${playerId}`),
    // Подія UpgradeCompleted може не дійти (обрив хабу) — дедлайн будівництва перепитуємо й самі
    // Так само момент самовідновлення: після нього темп і серія інші
    // Автозбір щохвилини поповнює склад на сервері — ресурси в шапці перечитуємо не рідше
    refetchInterval: (query) => {
      const due = atDue(query);
      return due === false ? AUTO_COLLECT_MS : Math.min(due, AUTO_COLLECT_MS);
    },
  });
}

function useBuildingAction(
  playerId: string,
  action: "upgrade" | "speedup",
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

export function useUpgradeBuilding(playerId: string) {
  return useBuildingAction(playerId, "upgrade", ["village"]);
}

/** Прискорення платить gems з гаманця акаунта. */
export function useSpeedUpBuilding(playerId: string) {
  return useBuildingAction(playerId, "speedup", ["village", "wallet"]);
}

/** Переставити будівлю на плані села: вузол сітки всередині муру, без накладання (рішення 13.10.2026). */
export function useMoveBuilding(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ buildingId, x, y }: { buildingId: string; x: number; y: number }) =>
      api<void>(`/api/village/${playerId}/buildings/${buildingId}/move`, {
        method: "POST",
        body: { x, y },
        idempotent: true,
      }),
    // Будівля лишається там, де її відпустили, не чекаючи відповіді; відмова сервера поверне її назад
    onMutate: ({ buildingId, x, y }) => {
      queryClient.setQueryData<VillageResponse>(queryKeys.village(playerId), (village) =>
        village === undefined
          ? village
          : { ...village, buildings: village.buildings.map((b) => (b.id === buildingId ? { ...b, x, y } : b)) },
      );
    },
    onSettled: () => invalidatePlayer(queryClient, playerId, ["village"]),
  });
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

