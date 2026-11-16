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
  | "lootboxPity"
  | "keyChests"
  | "beasts"
  | "research"
  | "banners"
  | "clan"
  | "clanHelp"
  | "clanRequests"
  | "power"
  | "dungeons"
  | "tower"
  | "towerLeaderboard"
  | "market"
  | "territory"
  | "clanTechnologies"
  | "clanShop";

/**
 * Усе, що може змінити видана нагорода: серед нагород бувають gems, ресурси, предмети, ключі скринь,
 * герої та їхні уламки, а новий герой чи спорядження міняє й силу. Один перелік на всі мутації з нагородами —
 * інакше кожна забувала щось своє (ключі скринь не оновлювались майже ніде).
 */
export const REWARD_SCOPES: readonly PlayerScope[] = ["wallet", "village", "inventory", "heroes", "keyChests", "power"];

/**
 * Інвалідація після мутації. Список — у самій мутації, а не в екрані:
 * екран не знає, що тренування списує ресурси села, а прискорення — gems.
 */
export function invalidatePlayer(queryClient: QueryClient, playerId: string, scopes: readonly PlayerScope[]): void {
  scopes.forEach((scope) => void queryClient.invalidateQueries({ queryKey: queryKeys[scope](playerId) }));
}
