import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type {
  BattleOdds,
  BattlePreviewResult,
  IncomingAttackResponse,
  MarchIntent,
  MarchResponse,
  MarchState,
  MarchTargetType,
  SendMarchRequest,
} from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";
import { refetchAtDue } from "./polling";

/** Значення enum-ів з контракту: числа на дроті, імена — тут. */
export const MARCH_STATE = {
  outbound: 1,
  returning: 2,
  completed: 3,
} as const satisfies Record<string, MarchState>;

export const MARCH_INTENT = {
  attack: 1,
  reinforce: 2,
  scout: 3,
} as const satisfies Record<string, MarchIntent>;

export const MARCH_TARGET = {
  monster: 1,
  village: 2,
  clanStructure: 3,
} as const satisfies Record<string, MarchTargetType>;

/** Смуга шансів: сервер навмисно не віддає числа, лише оцінку. */
export const BATTLE_ODDS: Record<BattleOdds, { label: string; className: string }> = {
  0: { label: "Розгромна перевага", className: "bg-emerald-100 text-emerald-800" },
  1: { label: "Перевага на вашому боці", className: "bg-lime-100 text-lime-800" },
  2: { label: "Сили рівні", className: "bg-amber-100 text-amber-800" },
  3: { label: "Ризиковано", className: "bg-orange-100 text-orange-800" },
  4: { label: "Безнадійно", className: "bg-red-100 text-red-800" },
};

export function useMarches(playerId: string): UseQueryResult<MarchResponse[]> {
  return useQuery({
    queryKey: queryKeys.marches(playerId),
    queryFn: () => api<MarchResponse[]>(`/api/marches/${playerId}`),
    // Прибуття й повернення обробляє сканер: перепитуємо на найближчий дедлайн
    refetchInterval: refetchAtDue<MarchResponse[]>((marches) => marches.map((march) => march.arrivesAt)),
  });
}

/**
 * Ворожі марші в дорозі на своє село, села соклановців і споруди клану.
 * Тривога приходить подією, а це — стан після перезавантаження; бій за прибуттям
 * проводить сканер, тож перепитуємо на найближчий дедлайн.
 */
export function useIncomingAttacks(playerId: string): UseQueryResult<IncomingAttackResponse[]> {
  return useQuery({
    queryKey: queryKeys.incoming(playerId),
    queryFn: () => api<IncomingAttackResponse[]>(`/api/marches/${playerId}/incoming`),
    refetchInterval: refetchAtDue<IncomingAttackResponse[]>((attacks) => attacks.map((attack) => attack.arrivesAt)),
  });
}

/** Прев'ю — POST без побічних ефектів: тіло те саме, що й у відправки. */
export function usePreviewMarch(playerId: string) {
  return useMutation({
    mutationFn: (request: SendMarchRequest) =>
      api<BattlePreviewResult>(`/api/marches/${playerId}/preview`, { method: "POST", body: request, idempotent: true }),
  });
}

/** Відправка знімає юнітів і героя з гарнізону; марш з'являється в списку. */
export function useSendMarch(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: SendMarchRequest) =>
      api<string>(`/api/marches/${playerId}`, { method: "POST", body: request, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["marches", "garrison", "heroes"]),
  });
}

/** Прискорення платить gems; бій відбудеться найближчим проходом сканера. */
export function useSpeedUpMarch(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (marchId: string) =>
      api<void>(`/api/marches/${playerId}/${marchId}/speedup`, { method: "POST", idempotent: true }),
    // Сервер завершує похід одразу: бій, повернення армії й героя видно в тій самій відповіді
    onSuccess: () => {
      invalidatePlayer(queryClient, playerId, ["marches", "wallet", "garrison", "heroes", "village", "battleReports", "quests"]);
      void queryClient.invalidateQueries({ queryKey: ["map"] });
    },
  });
}
