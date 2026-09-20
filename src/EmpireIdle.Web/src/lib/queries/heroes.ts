import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";

export type HeroesOverview = components["schemas"]["HeroesOverview"];
export type HeroSummary = components["schemas"]["HeroSummary"];
export type HeroShardSummary = components["schemas"]["HeroShardSummary"];

export function useHeroes(playerId: string): UseQueryResult<HeroesOverview> {
  return useQuery({
    queryKey: queryKeys.heroes(playerId),
    queryFn: () => api<HeroesOverview>(`/api/heroes/${playerId}`),
  });
}

/** Дії героя тілом не користуються: усе, що потрібно серверу, є в маршруті. */
function useHeroAction(playerId: string, path: (heroId: string) => string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (heroId: string) => api<void>(path(heroId), { method: "POST", idempotent: true }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.heroes(playerId) }),
  });
}

export function useLevelUpHero(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/level-up`);
}

export function useEvolveHero(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/evolve`);
}

export function useAppointLeader(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/appoint-leader`);
}

export function useHealHero(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/heal`);
}

export function useSummonHero(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (heroKey: string) =>
      api<void>(`/api/heroes/${playerId}/summon`, { method: "POST", body: { heroKey }, idempotent: true }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.heroes(playerId) }),
  });
}

export function useBuyShards(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    // Ідемпотентність: повтор після обриву мережі не має списати золото двічі
    mutationFn: (input: { heroKey: string; count: number }) =>
      api<void>(`/api/heroes/${playerId}/shards/buy`, { method: "POST", body: input, idempotent: true }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.heroes(playerId) });
      // Уламки коштують золота зі складу села
      void queryClient.invalidateQueries({ queryKey: queryKeys.village(playerId) });
    },
  });
}
