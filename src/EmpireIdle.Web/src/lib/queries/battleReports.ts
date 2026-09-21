import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { BattleReportResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";

const REPORTS_TAKE = 20;

export function useBattleReports(playerId: string): UseQueryResult<BattleReportResponse[]> {
  return useQuery({
    queryKey: queryKeys.battleReports(playerId),
    queryFn: () => api<BattleReportResponse[]>(`/api/battle-reports/${playerId}?take=${REPORTS_TAKE}`),
  });
}

export function useMarkReportRead(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (reportId: string) =>
      api<void>(`/api/battle-reports/${playerId}/${reportId}/read`, { method: "POST", idempotent: true }),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["battleReports"]),
  });
}
