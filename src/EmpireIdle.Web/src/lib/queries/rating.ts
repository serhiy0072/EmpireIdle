import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { LeaderboardEntryResponse, PlayerRankResponse, PowerResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";

/** Скільки рядків топу тягнемо: одна сторінка без прокрутки за межі екрана. */
export const LEADERBOARD_SIZE = 100;

/**
 * Сила гравця. Сервер перераховує її подіями (тренування, бій, повернення
 * походу), тож клієнт лише інвалідує за тими самими подіями й мутаціями.
 */
export function usePower(playerId: string): UseQueryResult<PowerResponse> {
  return useQuery({
    queryKey: queryKeys.power(playerId),
    queryFn: () => api<PowerResponse>(`/api/power/${playerId}`),
    staleTime: 30_000,
  });
}

/** Топ світу перераховується щогодини — частіше перепитувати нема сенсу. */
export function useLeaderboard(): UseQueryResult<LeaderboardEntryResponse[]> {
  return useQuery({
    queryKey: queryKeys.leaderboard,
    queryFn: () => api<LeaderboardEntryResponse[]>(`/api/rating/top?count=${LEADERBOARD_SIZE}`),
    staleTime: 5 * 60_000,
  });
}

export function useMyRank(playerId: string): UseQueryResult<PlayerRankResponse> {
  return useQuery({
    queryKey: queryKeys.rank(playerId),
    queryFn: () => api<PlayerRankResponse>(`/api/rating/${playerId}`),
    staleTime: 5 * 60_000,
  });
}

/** Компактне число сили: 12.4K замість 12 431 — у шапці місця мало. */
export function compactPower(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 10_000) return `${(value / 1_000).toFixed(1)}K`;
  return Math.round(value).toLocaleString("uk-UA");
}
