/** Ключі кешу в одному місці: події реального часу інвалідують те саме, що читають екрани. */
export const queryKeys = {
  catalog: ["catalog"] as const,
  village: (playerId: string) => ["village", playerId] as const,
  heroes: (playerId: string) => ["heroes", playerId] as const,
  battleReports: (playerId: string) => ["battleReports", playerId] as const,
  banners: (playerId: string) => ["banners", playerId] as const,
  wallet: (playerId: string) => ["wallet", playerId] as const,
  garrison: (playerId: string) => ["garrison", playerId] as const,
  quests: (playerId: string) => ["quests", playerId] as const,
  serverQuests: (playerId: string) => ["serverQuests", playerId] as const,
  marches: (playerId: string) => ["marches", playerId] as const,
  tutorial: (playerId: string) => ["tutorial", playerId] as const,
  /** Ділянка мапи не належить гравцю: ключ — від центру й радіуса. */
  mapArea: (x: number, y: number, radius: number) => ["map", "area", x, y, radius] as const,
  mapCell: (x: number, y: number) => ["map", "cell", x, y] as const,
};
