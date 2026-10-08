import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api, isApiError } from "../api";
import type { GiftItemRequest, InventoryResponse, UseItemRequest } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";
import { refetchAtDue } from "./polling";

export function useInventory(playerId: string): UseQueryResult<InventoryResponse> {
  return useQuery({
    queryKey: queryKeys.inventory(playerId),
    queryFn: () => api<InventoryResponse>(`/api/inventory/${playerId}`),
    // Бусти гаснуть на сервері без події: перепитуємо на найближчий кінець дії
    refetchInterval: refetchAtDue<InventoryResponse>((inventory) =>
      inventory.activeEffects.map((effect) => effect.expiresAt),
    ),
  });
}

/** Скільки разів пробуємо телепорт, клітину якому обирає гра, якщо її щойно зайняв інший гравець. */
const GAME_PICKED_CELL_ATTEMPTS = 3;

/** Ящик наливає ресурси в село, буст з'являється в інвентарі, телепорт рухає село мапою. */
export function useUseItem(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: UseItemRequest) => {
      // Клітину обирає гра (випадковий, до лідера): двох одночасних гравців на одну клітину розводить
      // індекс мапи — другий отримує 409, а повтор із новим ключем дістане наступну вільну (GDD §8.9).
      // Відмова відкочує команду цілком, тож повтор нічого не списує вдруге
      for (let attempt = 1; ; attempt++) {
        try {
          return await api<void>(`/api/inventory/${playerId}/use`, { method: "POST", body: input, idempotent: true });
        } catch (error: unknown) {
          const cellTaken = input.targetX == null && isApiError(error) && error.is("AlreadyExists");

          if (!cellTaken || attempt >= GAME_PICKED_CELL_ATTEMPTS) throw error;
        }
      }
    },
    onSuccess: () => {
      // Телепорт одразу повертає додому всі війська — з маршів і з чужих гарнізонів
      invalidatePlayer(queryClient, playerId, ["inventory", "village", "heroes", "garrison", "marches", "power"]);
      void queryClient.invalidateQueries({ queryKey: ["map"] });
    },
  });
}

/** Подарунок члену свого клану: предмет іде з інвентаря одразу, отримувач побачить його в своєму. */
export function useGiftItem(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: GiftItemRequest) =>
      api<void>(`/api/inventory/${playerId}/gift`, { method: "POST", body: input, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory"]),
  });
}

/** Прокачка артефакта коштує золота села; результат видно в інвентарі. */
export function useUpgradeArtifact(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (equipmentId: string) =>
      api<void>(`/api/inventory/${playerId}/equipment/${equipmentId}/upgrade`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory", "village"]),
  });
}

/** Одягання живе в маршруті героїв, але міняє інвентар: інвалідуємо обидва. */
export function useEquip(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    // Слот обирає сервер за типом предмета: намисто — у слот намиста
    mutationFn: (input: { heroId: string; equipmentId: string }) =>
      api<void>(`/api/heroes/${playerId}/${input.heroId}/equipment/${input.equipmentId}`, {
        method: "POST",
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory", "heroes", "power"]),
  });
}

export function useUnequip(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (equipmentId: string) =>
      api<void>(`/api/heroes/${playerId}/equipment/${equipmentId}`, { method: "DELETE", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory", "heroes", "power"]),
  });
}

/** «Швидке використання»: сервер вдягає найкраще вільне в кожен слот; відповідь — скільки вдягнуто. */
export function useEquipBest(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (heroId: string) =>
      api<{ equipped: number }>(`/api/heroes/${playerId}/${heroId}/equipment/best`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory", "heroes", "power"]),
  });
}

/** «Зняти все»: по одному запиту на предмет — слотів п'ять, окрема серверна команда не варта того. */
export function useUnequipAll(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (equipmentIds: string[]) => {
      for (const id of equipmentIds)
        await api<void>(`/api/heroes/${playerId}/equipment/${id}`, { method: "DELETE", idempotent: true });
    },
    // І після часткової невдачі: те, що встигли зняти, уже зняте
    onSettled: () => invalidatePlayer(queryClient, playerId, ["inventory", "heroes", "power"]),
  });
}

/** Таймер прискорення на сервері (SpeedUpTimer): число в запиті, назва — у межах каталогу. */
export const SPEED_UP_TIMER_IDS = { Construction: 0, Training: 1, UnitLevelUp: 2, March: 3 } as const;

/** Прискорити таймер предметами: хвилини складаються, надлишок понад залишок згорає. */
export function useSpeedUpWithItems(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { timer: keyof typeof SPEED_UP_TIMER_IDS; targetId: string; items: Record<string, number> }) =>
      api<void>(`/api/inventory/${playerId}/speedups`, {
        method: "POST",
        body: { timer: SPEED_UP_TIMER_IDS[input.timer], targetId: input.targetId, items: input.items },
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory", "village", "garrison", "marches", "power"]),
  });
}
