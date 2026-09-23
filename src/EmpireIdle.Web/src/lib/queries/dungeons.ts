import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { DungeonRunView, DungeonsOverview } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

/** Вітрина данжів: енергія відновлюється часом, тож дані швидко старіють. */
export function useDungeons(playerId: string): UseQueryResult<DungeonsOverview> {
  return useQuery({
    queryKey: queryKeys.dungeons(playerId),
    queryFn: () => api<DungeonsOverview>(`/api/dungeons/${playerId}`),
    staleTime: 30_000,
  });
}

/**
 * Незавершений забіг. Потрібен після перезавантаження сторінки: бій живе
 * на сервері, тож клієнт відновлює його, а не тримає у вкладці.
 */
export function useDungeonRun(playerId: string, enabled: boolean): UseQueryResult<DungeonRunView | null> {
  return useQuery({
    queryKey: queryKeys.dungeonRun(playerId),
    queryFn: async () => (await api<DungeonRunView | undefined>(`/api/dungeons/${playerId}/run`)) ?? null,
    enabled,
  });
}

export function useStartDungeonRun(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { dungeonKey: string; level: number; heroIds: string[] }) =>
      api<DungeonRunView>(`/api/dungeons/${playerId}/run`, { method: "POST", body: input, idempotent: true }),
    onSuccess: (run) => {
      // Забіг уже приїхав повністю — кладемо його в кеш, щоб бій почався без другого запиту
      queryClient.setQueryData(queryKeys.dungeonRun(playerId), run);
      invalidatePlayer(queryClient, playerId, ["dungeons"]);
    },
  });
}

/**
 * Один хід. Сервер повертає той самий DungeonRunView, що й перегляд, разом
 * із журналом зроблених ходів — клієнт малює його й нічого не зшиває сам.
 */
export function useDungeonTurn(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { runId: string; auto: boolean; abilityKey?: string | null; targetIndex?: number | null }) =>
      api<DungeonRunView>(`/api/dungeons/${playerId}/run/${input.runId}/turn`, {
        method: "POST",
        body: { auto: input.auto, abilityKey: input.abilityKey ?? null, targetIndex: input.targetIndex ?? null },
      }),
    onSuccess: (result) => {
      if (result.state !== "InProgress") {
        // Забіг скінчився: нагорода вже в селі й інвентарі, сила змінилась
        invalidatePlayer(queryClient, playerId, ["dungeons", "inventory", "village", "power"]);
        void queryClient.invalidateQueries({ queryKey: queryKeys.dungeonRun(playerId) });
      }
    },
  });
}

export function useAbandonDungeonRun(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (runId: string) =>
      api<void>(`/api/dungeons/${playerId}/run/${runId}/abandon`, { method: "POST", idempotent: true }),
    onSuccess: () => {
      queryClient.setQueryData(queryKeys.dungeonRun(playerId), null);
      invalidatePlayer(queryClient, playerId, ["dungeons"]);
    },
  });
}

const RARITY_LABELS: Record<string, string> = {
  common: "звичайний",
  rare: "рідкісний",
  unique: "унікальний",
};

export function artifactRarityLabel(rarity: string): string {
  return RARITY_LABELS[rarity] ?? rarity;
}

const STATUS_LABELS: Record<string, string> = {
  Poison: "отрута",
  Stun: "оглушення",
  AttackUp: "атака ↑",
  DefenseUp: "захист ↑",
  DefenseDown: "захист ↓",
  Shield: "щит",
  Taunt: "провокація",
};

export function statusLabel(kind: string): string {
  return STATUS_LABELS[kind] ?? kind;
}
