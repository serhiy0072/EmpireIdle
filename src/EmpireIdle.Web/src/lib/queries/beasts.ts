import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";
import { refetchAtDue } from "./polling";

export type BeastPenResponse = components["schemas"]["BeastPenResponse"];
export type BeastResponse = components["schemas"]["BeastResponse"];
export type BeastTamingResponse = components["schemas"]["BeastTamingResponse"];

/** Звіринець (GDD §5.10): місця, звірі з рівнями й пасивками, шанси приручення. */
export function useBeastPen(playerId: string): UseQueryResult<BeastPenResponse> {
  return useQuery({
    queryKey: queryKeys.beasts(playerId),
    queryFn: () => api<BeastPenResponse>(`/api/beasts/${playerId}`),
    enabled: playerId !== "",
    // Дія пасивки й перезарядка спливають самі: перечитуємо на найближчий майбутній дедлайн.
    // Минулі відкидаємо — на відміну від черг, вони лишаються у відповіді назавжди
    refetchInterval: refetchAtDue<BeastPenResponse>((pen) =>
      pen.beasts
        .flatMap((beast) => [beast.passive.activeUntil, beast.passive.cooldownUntil])
        .filter((at) => at != null && Date.parse(at) > Date.now()),
    ),
  });
}

/** Годування списує корм з інвентаря. */
export function useFeedBeast(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { beastKey: string; count: number }) =>
      api<void>(`/api/beasts/${playerId}/${input.beastKey}/feed?count=${input.count}`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["beasts", "inventory"]),
  });
}

/** Активація списує їжу зі складу; бонус пасивки змінює силу й виробіток. */
export function useActivateBeast(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (beastKey: string) =>
      api<void>(`/api/beasts/${playerId}/${beastKey}/activate`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["beasts", "village", "power"]),
  });
}
