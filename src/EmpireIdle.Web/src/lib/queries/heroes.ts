import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";

export type HeroesOverview = components["schemas"]["HeroesOverview"];
export type HeroSummary = components["schemas"]["HeroSummary"];
export type HeroShardSummary = components["schemas"]["HeroShardSummary"];

export function useHeroes(playerId: string): UseQueryResult<HeroesOverview> {
  return useQuery({
    queryKey: queryKeys.heroes(playerId),
    queryFn: () => api<HeroesOverview>(`/api/heroes/${playerId}`),
    // Чергу прокачки завершує сканер на сервері: перепитуємо на її дедлайн
    refetchInterval: refetchAtDue<HeroesOverview>((overview) => [overview.activeOrder?.completesAt]),
  });
}

/** Дії героя тілом не користуються: усе, що потрібно серверу, є в маршруті. */
function useHeroAction(playerId: string, path: (heroId: string) => string, scopes: readonly PlayerScope[]) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (heroId: string) => api<void>(path(heroId), { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, scopes),
  });
}

/** Прокачка коштує ресурсів села. */
export function useLevelUpHero(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/level-up`, ["heroes", "village"]);
}

export function useEvolveHero(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/evolve`, ["heroes"]);
}

export function useAppointLeader(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/appoint-leader`, ["heroes"]);
}

/** Лікування героя списує ресурси села. */
export function useHealHero(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/heal`, ["heroes", "village"]);
}

export function useSummonHero(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (heroKey: string) =>
      api<void>(`/api/heroes/${playerId}/summon`, { method: "POST", body: { heroKey }, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes"]),
  });
}

export function useBuyShards(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    // Ідемпотентність: повтор після обриву мережі не має списати золото двічі
    mutationFn: (input: { heroKey: string; count: number }) =>
      api<void>(`/api/heroes/${playerId}/shards/buy`, { method: "POST", body: input, idempotent: true }),
    // Уламки коштують золота зі складу села
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "village"]),
  });
}
