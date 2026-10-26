import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { BattleReplay, ManualCast, TowerLeaderboard, TowerStormResult, TowerView } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

/** Екран вежі: прогрес, ворог наступного поверху, нагороди й бафи — бафи спливають, тож дані швидко старіють. */
export function useTower(playerId: string): UseQueryResult<TowerView> {
  return useQuery({
    queryKey: queryKeys.tower(playerId),
    queryFn: () => api<TowerView>(`/api/tower/${playerId}`),
    staleTime: 30_000,
  });
}

/** Рейтинг вежі на сервері й власний рядок. */
export function useTowerLeaderboard(playerId: string, enabled: boolean): UseQueryResult<TowerLeaderboard> {
  return useQuery({
    queryKey: queryKeys.towerLeaderboard(playerId),
    queryFn: () => api<TowerLeaderboard>(`/api/tower/${playerId}/leaderboard`),
    enabled,
    staleTime: 60_000,
  });
}

/**
 * Штурм поверху. Сервер рахує бій до кінця й віддає лог — клієнт лише програє його.
 * Перемога несе нагороду в село й інвентар; поразка нічого не змінює, тож і перечитувати нічого.
 */
export function useStormTowerFloor(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { floor: number; lanes: (string | null)[]; casts?: ManualCast[] | null }) =>
      api<TowerStormResult>(`/api/tower/${playerId}/storm`, { method: "POST", body: input, idempotent: true }),
    onSuccess: (result) => {
      if (result.victory)
        // Досвід героям лягає в запас на екрані героїв
        invalidatePlayer(queryClient, playerId, ["tower", "towerLeaderboard", "village", "wallet", "inventory", "heroes"]);
    },
  });
}

/**
 * Ручний бій: сервер рахує бій з усіма наказами від початку й нічого не зберігає. Кожне натискання вміння —
 * новий перегляд; зарахує бій лише штурм із тими самими наказами.
 */
export function usePreviewTowerFloor(playerId: string) {
  return useMutation({
    mutationFn: (input: { floor: number; lanes: (string | null)[]; casts: ManualCast[] }) =>
      api<BattleReplay>(`/api/tower/${playerId}/preview`, { method: "POST", body: input }),
  });
}
