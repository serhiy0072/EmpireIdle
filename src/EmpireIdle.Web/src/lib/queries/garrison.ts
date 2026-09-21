import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { GarrisonResponse, HealPaymentMethod } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";

/** Значення HealPaymentMethod з контракту: числа на дроті, імена — тут. */
export const HEAL_PAYMENT = {
  resources: 1,
  gems: 2,
} as const satisfies Record<string, HealPaymentMethod>;

export function useGarrison(playerId: string): UseQueryResult<GarrisonResponse> {
  return useQuery({
    queryKey: queryKeys.garrison(playerId),
    queryFn: () => api<GarrisonResponse>(`/api/garrisons/${playerId}`),
    // Черги завершує сканер без події клієнту, а викуп згорає за дедлайном
    refetchInterval: refetchAtDue<GarrisonResponse>((garrison) => [
      ...garrison.trainingOrders.map((order) => order.completesAt),
      ...garrison.levelUpOrders.map((order) => order.completesAt),
      ...garrison.recoverable.map((stack) => stack.expiresAt),
    ]),
  });
}

/** Команди гарнізону: маршрут будується з запиту, тіло йде лише в об'єктних запитів. */
function useGarrisonCommand<TRequest>(
  playerId: string,
  path: (request: TRequest) => string,
  scopes: readonly PlayerScope[],
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: TRequest) =>
      api<void>(path(request), {
        method: "POST",
        body: typeof request === "string" ? undefined : request,
        idempotent: true,
      }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, scopes),
  });
}

/** Тренування списує ресурси села. */
export function useTrainUnits(playerId: string) {
  return useGarrisonCommand<{ unitType: string; level: number; count: number }>(
    playerId,
    () => `/api/garrisons/${playerId}/units/train`,
    ["garrison", "village"],
  );
}

/** Прискорення платить gems. */
export function useSpeedUpTraining(playerId: string) {
  return useGarrisonCommand<string>(
    playerId,
    (orderId) => `/api/garrisons/${playerId}/training/${orderId}/speedup`,
    ["garrison", "wallet"],
  );
}

/** Прокачка списує ресурси села. */
export function useLevelUpUnits(playerId: string) {
  return useGarrisonCommand<{ unitType: string; fromLevel: number; toLevel: number; count: number }>(
    playerId,
    () => `/api/garrisons/${playerId}/units/levelup`,
    ["garrison", "village"],
  );
}

export function useSpeedUpLevelUp(playerId: string) {
  return useGarrisonCommand<string>(
    playerId,
    (orderId) => `/api/garrisons/${playerId}/levelup/${orderId}/speedup`,
    ["garrison", "wallet"],
  );
}

/** Ключі — рядки "unitType@level" (wire-формат UnitStackKey). Платить село або гаманець — інвалідуємо обидва. */
export function useHealWounded(playerId: string) {
  return useGarrisonCommand<{ units: Record<string, number>; payment: HealPaymentMethod }>(
    playerId,
    () => `/api/garrisons/${playerId}/units/heal`,
    ["garrison", "village", "wallet"],
  );
}

/** Ключі — рядки "unitType@level" (wire-формат UnitStackKey). Викуп завжди за gems. */
export function useRecoverUnits(playerId: string) {
  return useGarrisonCommand<{ units: Record<string, number> }>(
    playerId,
    () => `/api/garrisons/${playerId}/units/recover`,
    ["garrison", "wallet"],
  );
}
