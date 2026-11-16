import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { ClanOfferingResult, ClanShopKind, ClanShopView, ClanTechnologiesView } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, REWARD_SCOPES, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";

/** Технологія клану так, як її приймає сервер: тір і ключ вузла. */
export interface ClanTechnologyTarget {
  tier: number;
  node: string;
}

/**
 * Дерево клану з моїми спробами й монетами. Поза кланом сервер відповідає порожнім тілом —
 * це стан, а не помилка, тож зводимо його до null, як і useMyClan.
 */
export function useClanTechnologies(playerId: string, enabled: boolean): UseQueryResult<ClanTechnologiesView | null> {
  return useQuery({
    queryKey: queryKeys.clanTechnologies(playerId),
    queryFn: async () => (await api<ClanTechnologiesView | null | undefined>(`/api/clan-technologies/${playerId}`)) ?? null,
    enabled,
    // Спроба відновлюється за часом без події
    refetchInterval: refetchAtDue<ClanTechnologiesView | null>((view) =>
      view?.offering.nextAttemptAt == null ? [] : [view.offering.nextAttemptAt],
    ),
  });
}

/**
 * Підношення списує ресурс села чи gems, а денні пороги видають нагороди будь-якого типу.
 * Новий рівень технології клану змінює виробіток і силу всіх учасників.
 */
const OFFERING_SCOPES: readonly PlayerScope[] = ["clanTechnologies", "clanShop", ...REWARD_SCOPES];

export function useOfferClanResources(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (target: ClanTechnologyTarget) =>
      api<ClanOfferingResult>(`/api/clan-technologies/${playerId}/offer-resources`, { method: "POST", body: target, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, OFFERING_SCOPES),
  });
}

/** Ціну в gems клієнт передає ту, яку бачив: подорожчала — сервер відмовить, а не спише більше. */
export function useOfferClanGems(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ClanTechnologyTarget & { expectedPrice: number }) =>
      api<ClanOfferingResult>(`/api/clan-technologies/${playerId}/offer-gems`, { method: "POST", body: request, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, OFFERING_SCOPES),
  });
}

/** Мітка керівництва: null знімає її. */
export function useMarkClanTechnology(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (target: ClanTechnologyTarget | null) =>
      api<void>(`/api/clan-technologies/${playerId}/mark`, {
        method: "POST",
        body: { tier: target?.tier ?? null, node: target?.node ?? null },
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["clanTechnologies"]),
  });
}

/** Вітрини оновлюються за розкладом без події: перепитуємо, щойно настане найближче оновлення. */
export function useClanShop(playerId: string): UseQueryResult<ClanShopView> {
  return useQuery({
    queryKey: queryKeys.clanShop(playerId),
    queryFn: () => api<ClanShopView>(`/api/clan-technologies/${playerId}/shop`),
    refetchInterval: refetchAtDue<ClanShopView>((shop) => [shop.daily.refreshesAt, shop.weekly.refreshesAt, shop.free.refreshesAt]),
  });
}

/** Покупка за кланові монети: ціну шлемо ту, яку бачив гравець, — знижка могла змінитися з новою вітриною. */
export function useBuyClanShopItem(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: { kind: ClanShopKind; itemKey: string; quantity: number; expectedPrice: number }) =>
      api<void>(`/api/clan-technologies/${playerId}/shop/buy`, { method: "POST", body: request, idempotent: true }),
    // Серед товарів є ключі скринь
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["clanShop", "clanTechnologies", "inventory", "keyChests"]),
  });
}

export function useClaimClanFreeItem(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: { itemKey: string; quantity: number }) =>
      api<void>(`/api/clan-technologies/${playerId}/shop/claim-free`, { method: "POST", body: request, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["clanShop", "inventory"]),
  });
}
