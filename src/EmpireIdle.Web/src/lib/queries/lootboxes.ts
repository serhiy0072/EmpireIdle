import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { LootboxOpening, LootboxPityView, LootboxPurchase } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";

/** Зі скрині випадає будь-що: gems, предмети, ресурси села, досвід і осколки героїв. */
const OPENED: readonly PlayerScope[] = ["wallet", "inventory", "village", "heroes", "lootboxPity"];

/** Скільки до гарантії в кожній скрині з гарантією. */
export function useLootboxPity(playerId: string): UseQueryResult<LootboxPityView[]> {
  return useQuery({
    queryKey: queryKeys.lootboxPity(playerId),
    queryFn: () => api<LootboxPityView[]>(`/api/inventory/${playerId}/lootboxes/pity`),
    enabled: playerId !== "",
  });
}

/** Відкрити скрині з рюкзака: відповідь — що випало. */
export function useOpenLootbox(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { itemKey: string; count: number }) =>
      api<LootboxOpening>(`/api/inventory/${playerId}/lootboxes/${input.itemKey}/open?count=${input.count}`, {
        method: "POST",
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, OPENED),
  });
}

/** Купити скрині в крамниці — вони відкриваються одразу, у рюкзак не лягають. */
export function useBuyLootbox(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { itemKey: string; count: number }) =>
      api<LootboxPurchase>(`/api/shop/${playerId}/lootboxes/${input.itemKey}?count=${input.count}`, {
        method: "POST",
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, OPENED),
  });
}
