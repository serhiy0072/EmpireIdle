import type { QueryClient } from "@tanstack/react-query";
import { queryKeys } from "../queryKeys";

/** Що зачепила команда: кожна мутація перелічує всі агрегати, які вона змінює на сервері. */
export type PlayerScope =
  | "village"
  | "wallet"
  | "garrison"
  | "heroes"
  | "quests"
  | "serverQuests"
  | "marches"
  | "battleReports"
  | "tutorial"
  | "inventory"
  | "banners"
  | "clan"
  | "clanHelp"
  | "clanRequests"
  | "power";

/**
 * Інвалідація після мутації. Список — у самій мутації, а не в екрані:
 * екран не знає, що тренування списує ресурси села, а прискорення — gems.
 */
export function invalidatePlayer(queryClient: QueryClient, playerId: string, scopes: readonly PlayerScope[]): void {
  scopes.forEach((scope) => void queryClient.invalidateQueries({ queryKey: queryKeys[scope](playerId) }));
}
