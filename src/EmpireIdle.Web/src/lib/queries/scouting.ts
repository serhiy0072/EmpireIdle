import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { SendScoutRequest } from "../apiTypes";
import { invalidatePlayer } from "./invalidate";

/** Розвідники йдуть без героя й юнітів — гарнізон і герої не змінюються, лише список маршів. */
export function useSendScout(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: SendScoutRequest) =>
      api<string>(`/api/marches/${playerId}/scout`, { method: "POST", body: request, idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["marches"]),
  });
}

/** Підписи результатів розвідки для гравця. */
export const SCOUT_OUTCOMES: Record<string, string> = {
  Success: "Розвідано",
  Blocked: "Село сховане від розвідки",
  TargetMoved: "Ціль переселилась — на місці порожньо",
  TargetGone: "Цілі вже немає",
};
