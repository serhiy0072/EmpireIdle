import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { SendScoutRequest } from "../apiTypes";
import { invalidatePlayer } from "./invalidate";
import { t } from "i18next";

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
const SCOUT_OUTCOMES = ["Success", "Blocked", "TargetMoved", "TargetGone"] as const;

export function scoutOutcomeLabel(outcome: string): string {
  return (SCOUT_OUTCOMES as readonly string[]).includes(outcome)
    ? t(`map:scoutOutcome.${outcome as (typeof SCOUT_OUTCOMES)[number]}`)
    : outcome;
}
