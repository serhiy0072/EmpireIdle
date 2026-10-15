import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";

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
function useHeroAction(playerId: string, path: (heroId: string) => string, scopes: readonly PlayerScope[]) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (heroId: string) => api<void>(path(heroId), { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, scopes),
  });
}

/** Рівень піднімається одразу за досвід із пулу гравця (GDD §6.1) — без черги й таймера. */
export function useLevelUpHero(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ heroId, levels }: { heroId: string; levels: number }) =>
      api<void>(`/api/heroes/${playerId}/${heroId}/level-up?levels=${levels}`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "power"]),
  });
}

/** Вміння героя на рівень за книгу його ролі, рідкості й половини (GDD §6.1). */
export function useUpgradeHeroSkill(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ heroId, skillKey }: { heroId: string; skillKey: string }) =>
      api<void>(`/api/heroes/${playerId}/${heroId}/skills/${encodeURIComponent(skillKey)}/upgrade`, {
        method: "POST",
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "inventory", "power"]),
  });
}

/** Навчальний табір (GDD §6.1): герой отримує рівень найслабшого з п'ятірки найсильніших поза табором. */
export function usePlaceInCamp(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ heroId, slot }: { heroId: string; slot: number }) =>
      api<void>(`/api/heroes/${playerId}/camp/slots/${slot}/heroes/${heroId}`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "power"]),
  });
}

export function useRemoveFromCamp(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/camp/leave`, ["heroes", "power"]);
}

export function useSkipCampCooldown(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (slot: number) =>
      api<void>(`/api/heroes/${playerId}/camp/slots/${slot}/skip-cooldown`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "wallet"]),
  });
}

export function useBuyCampSlot(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api<void>(`/api/heroes/${playerId}/camp/slots`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "wallet"]),
  });
}

/** Скидання на перший рівень: весь досвід повертається в пул. */
export function useResetHeroLevel(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/reset-level`, ["heroes", "power"]);
}

export function useEvolveHero(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/evolve`, ["heroes", "power"]);
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

/** Заповнити наступну частинку зірки за осколки героя (GDD §6.1). */
export function useAdvanceHeroStar(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/star`, ["heroes", "power"]);
}

/** Зброя героя (GDD §6.4): наступний рівень за шматки, що лежать на герої. */
export function useUpgradeWeapon(playerId: string) {
  return useHeroAction(playerId, (heroId) => `/api/heroes/${playerId}/${heroId}/weapon`, ["heroes", "power"]);
}

/** Скрині зброї з рюкзака — у шматки зброї обраного героя з трійки скрині. */
export function useOpenWeaponChests(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { heroId: string; itemKey: string; count: number }) =>
      api<void>(`/api/heroes/${playerId}/${input.heroId}/weapon/chests`, {
        method: "POST",
        body: { itemKey: input.itemKey, count: input.count },
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "inventory"]),
  });
}

/** Шматки зброї звичайного чи рідкісного героя за gems; у унікальних — лише зі скринь зброї. */
export function useBuyWeaponShards(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { heroId: string; count: number }) =>
      api<number>(`/api/shop/${playerId}/heroes/${input.heroId}/weapon-shards?count=${input.count}`, {
        method: "POST",
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "wallet"]),
  });
}

/** Універсальні осколки — в осколки відкритого героя 1:1. */
export function useConvertUniversalShards(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { heroKey: string; count: number }) =>
      api<void>(`/api/heroes/${playerId}/shards/convert`, { method: "POST", body: input, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["heroes", "inventory"]),
  });
}

/** Обмін універсальних на рідкість вищу: 100 звичайних → 1 рідкісний, 300 рідкісних → 1 унікальний. */
/** Значення Rarity з контракту: на дроті — числа, імена — тут (рядок сервер не розбирає й відповідає 400). */
const SHARD_RARITY = {
  Common: 1,
  Rare: 2,
} as const satisfies Record<string, components["schemas"]["Rarity"]>;

export function useUpgradeUniversalShards(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { from: keyof typeof SHARD_RARITY; count: number }) =>
      api<void>(`/api/heroes/${playerId}/shards/upgrade`, {
        method: "POST",
        body: { from: SHARD_RARITY[input.from], count: input.count },
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory"]),
  });
}
