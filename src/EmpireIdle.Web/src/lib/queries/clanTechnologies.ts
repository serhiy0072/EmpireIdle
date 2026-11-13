import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { ClanOfferingResult, ClanTechnologiesView } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
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
const OFFERING_SCOPES: readonly PlayerScope[] = ["clanTechnologies", "village", "wallet", "inventory", "heroes", "power"];

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
