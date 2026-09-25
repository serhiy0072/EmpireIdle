import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { ClanTerritoryResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";

/**
 * Територія клану гравця (GDD §7.2). Поза кланом сервер віддає null — це стан, а не помилка.
 * Споруда запрацьовує за часом, без події: перечитуємо в момент готовності.
 */
export function useClanTerritory(playerId: string, enabled = true): UseQueryResult<ClanTerritoryResponse | null> {
  return useQuery({
    queryKey: queryKeys.territory(playerId),
    queryFn: async () => (await api<ClanTerritoryResponse | undefined>(`/api/territory/${playerId}`)) ?? null,
    enabled,
    // Лише майбутні моменти: добудована споруда тримала б минулий readyAt вічно,
    // і refetchAtDue вважав би його простроченим, опитуючи кожні кілька секунд
    refetchInterval: refetchAtDue<ClanTerritoryResponse | null>(
      (view) => view?.structures.filter((s) => Date.parse(s.readyAt) > Date.now()).map((s) => s.readyAt) ?? [],
    ),
  });
}

function useTerritoryCommand<TInput>(
  playerId: string,
  request: (input: TInput) => { path: string; body?: unknown },
  scopes: readonly PlayerScope[],
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: TInput) => {
      const { path, body } = request(input);
      return api<unknown>(path, { method: "POST", body, idempotent: true });
    },
    onSuccess: () => {
      invalidatePlayer(queryClient, playerId, scopes);
      // Споруда займає чи звільняє клітину — мапа спільна для всіх
      void queryClient.invalidateQueries({ queryKey: ["map"] });
    },
  });
}

export function usePlaceStructure(playerId: string) {
  return useTerritoryCommand<{ x: number; y: number }>(
    playerId,
    (cell) => ({ path: `/api/territory/${playerId}/structures`, body: cell }),
    ["territory"],
  );
}

export function useDemolishStructure(playerId: string) {
  return useTerritoryCommand<string>(
    playerId,
    (structureId) => ({ path: `/api/territory/${playerId}/structures/${structureId}/demolish` }),
    ["territory", "marches"],
  );
}

export function useRecallFromStructure(playerId: string) {
  return useTerritoryCommand<string>(
    playerId,
    (structureId) => ({ path: `/api/territory/${playerId}/structures/${structureId}/recall` }),
    ["territory", "marches", "heroes"],
  );
}
