import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { KeyChestView, LootboxOpening, LootboxPityView, LootboxPurchase } from "../apiTypes";
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

/** Сервер відкриває щонайбільше стільки скринь за запит (OpenLootboxCommand.MaxCount). */
const MAX_PER_OPEN = 100;

/**
 * Відкрити скрині з рюкзака: відповідь — що випало. Більше за MAX_PER_OPEN відкриваються кількома
 * запитами поспіль (рішення 22.10.2026: «усі» — це весь стек). Якщо запит посередині впав, гравець
 * бачить те, що вже відкрилось, а не помилку: ті скрині вже списані.
 */
export function useOpenLootbox(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: { itemKey: string; count: number }): Promise<LootboxOpening> => {
      const opened: LootboxOpening = { itemKey: input.itemKey, drops: [] };

      for (let left = input.count; left > 0; left -= MAX_PER_OPEN) {
        const count = Math.min(left, MAX_PER_OPEN);

        try {
          const part = await api<LootboxOpening>(`/api/inventory/${playerId}/lootboxes/${input.itemKey}/open?count=${count}`, {
            method: "POST",
            idempotent: true,
          });
          opened.drops.push(...part.drops);
        } catch (error) {
          if (opened.drops.length === 0) throw error;
          break;
        }
      }

      return opened;
    },
    onSettled: () => invalidatePlayer(queryClient, playerId, OPENED),
  });
}

/** Ключові скрині: ключі, залишок безкоштовних відкриттів і коли наступне. */
export function useKeyChests(playerId: string): UseQueryResult<KeyChestView[]> {
  return useQuery({
    queryKey: queryKeys.keyChests(playerId),
    queryFn: () => api<KeyChestView[]>(`/api/inventory/${playerId}/key-chests`),
    enabled: playerId !== "",
  });
}

/** Відкрити ключову скриню: безкоштовно — одну, ключами — count. */
export function useOpenKeyChest(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { chestKey: string; free: boolean; count: number }) =>
      api<LootboxOpening>(
        `/api/inventory/${playerId}/key-chests/${input.chestKey}/open?free=${input.free}&count=${input.count}`,
        { method: "POST", idempotent: true },
      ),
    onSuccess: () => invalidatePlayer(queryClient, playerId, [...OPENED, "keyChests"]),
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
