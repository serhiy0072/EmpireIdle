import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { EnhancementResponse, GiftItemRequest, InventoryResponse, UseItemRequest } from "../apiTypes";
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

/** Ящик наливає ресурси в село, буст з'являється в інвентарі, телепорт рухає село мапою. */
export function useUseItem(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: UseItemRequest) =>
      api<void>(`/api/inventory/${playerId}/use`, { method: "POST", body: input, idempotent: true }),
    onSuccess: () => {
      invalidatePlayer(queryClient, playerId, ["inventory", "village", "heroes"]);
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

/** Заточка коштує золота села; результат — success, failure або broken. */
export function useEnhance(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (equipmentId: string) =>
      api<EnhancementResponse>(`/api/inventory/${playerId}/equipment/${equipmentId}/enhance`, {
        method: "POST",
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory", "village"]),
  });
}

function useEquipmentAction(playerId: string, action: "repair" | "upgrade") {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (equipmentId: string) =>
      api<void>(`/api/inventory/${playerId}/equipment/${equipmentId}/${action}`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["inventory", "village"]),
  });
}

export function useRepair(playerId: string) {
  return useEquipmentAction(playerId, "repair");
}

export function useUpgradeArtifact(playerId: string) {
  return useEquipmentAction(playerId, "upgrade");
}

/** Зброя з каталогу за золото села. */
export function useBuyWeapon(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (itemKey: string) =>
      api<void>(`/api/inventory/${playerId}/weapons/${itemKey}`, { method: "POST", idempotent: true }),
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
