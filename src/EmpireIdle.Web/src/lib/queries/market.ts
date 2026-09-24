import { keepPreviousData, useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

export type MarketListingView = components["schemas"]["MarketListingView"];
export type MarketPageView = components["schemas"]["MarketPageView"];
export type MyMarketView = components["schemas"]["MyMarketView"];
export type MarketQuoteView = components["schemas"]["MarketQuoteView"];

/** Вид товару в запитах — числом, як у enum сервера; у view він приходить рядком. */
export const MARKET_KIND = { Equipment: 1, Hero: 2, Item: 3 } as const;
export type MarketKind = (typeof MARKET_KIND)[keyof typeof MARKET_KIND];

/** Товар для котирування й виставлення: заповнюється поле свого виду. */
export interface MarketGoods {
  kind: MarketKind;
  equipmentId?: string;
  heroId?: string;
  itemKey?: string;
  quantity: number;
}

export const PAGE_SIZE = 20;

/** Вітрина: сторінка активних лотів; попередня сторінка лишається на екрані, поки вантажиться наступна. */
export function useMarketListings(
  playerId: string,
  kind: MarketKind | null,
  page: number,
): UseQueryResult<MarketPageView> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
  if (kind !== null) params.set("kind", String(kind));

  return useQuery({
    queryKey: queryKeys.marketBrowse(playerId, kind, page),
    queryFn: () => api<MarketPageView>(`/api/market/${playerId}?${params.toString()}`),
    placeholderData: keepPreviousData,
  });
}

export function useMyMarket(playerId: string): UseQueryResult<MyMarketView> {
  return useQuery({
    queryKey: queryKeys.market(playerId),
    queryFn: () => api<MyMarketView>(`/api/market/${playerId}/mine`),
  });
}

/**
 * Котирування — запит, а не мутація: той самий товар дає той самий діапазон,
 * і кеш не дає смикати сервер на кожне перемикання вибору.
 */
export function useMarketQuote(playerId: string, goods: MarketGoods | null): UseQueryResult<MarketQuoteView> {
  return useQuery({
    queryKey: queryKeys.marketQuote(playerId, goods),
    queryFn: () => api<MarketQuoteView>(`/api/market/${playerId}/quote`, { method: "POST", body: goods }),
    enabled: goods !== null,
    retry: false,
  });
}

/** Виставлення: товар іде в заставу, податок — із золота села. */
export function useListOnMarket(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: MarketGoods & { priceGold: number }) =>
      api<{ listingId: string }>(`/api/market/${playerId}/listings`, { method: "POST", body: input, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["market", "inventory", "heroes", "village", "power"]),
  });
}

/** Купівля: золото й товар переходять разом; вітрина теж змінюється. */
export function useBuyListing(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (listingId: string) =>
      api<void>(`/api/market/${playerId}/listings/${listingId}/buy`, { method: "POST", idempotent: true }),
    // Невдала купівля теж оновлює вітрину: найчастіше лот уже купив хтось інший
    onSettled: () => invalidatePlayer(queryClient, playerId, ["market", "inventory", "heroes", "village", "power"]),
  });
}

export function useCancelListing(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (listingId: string) =>
      api<void>(`/api/market/${playerId}/listings/${listingId}/cancel`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["market", "inventory", "heroes"]),
  });
}
