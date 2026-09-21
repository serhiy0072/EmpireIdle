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
};
