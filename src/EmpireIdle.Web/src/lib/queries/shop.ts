import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { ShopView } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

/** Асортимент спільний для всіх і йде з конфіга: старіє так само повільно, як каталог. */
export function useShop(): UseQueryResult<ShopView> {
  return useQuery({
    queryKey: queryKeys.shop,
    queryFn: () => api<ShopView>("/api/shop"),
    staleTime: 5 * 60_000,
  });
}

/** Покупка за gems: сервер повертає залишок, але гаманець і інвентар усе одно перечитуємо. */
export function useBuyShopItem(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { itemKey: string; count: number }) =>
      api<number>(`/api/shop/${playerId}/items/${input.itemKey}?count=${input.count}`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["wallet", "inventory"]),
  });
}

/** Пакет gems за гроші: сервер створює сесію оплати й віддає посилання на Stripe Checkout. */
export function useCheckout(playerId: string) {
  return useMutation({
    mutationFn: (packKey: string) =>
      api<{ checkoutUrl: string }>(`/api/payments/${playerId}/checkout/${packKey}`, { method: "POST" }),
  });
}
