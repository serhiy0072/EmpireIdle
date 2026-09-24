/** Ключі кешу в одному місці: події реального часу інвалідують те саме, що читають екрани. */
export const queryKeys = {
  /** Каталог — мовою гравця: інша мова, інші назви, інший ETag. */
  catalog: (language: string) => ["catalog", language] as const,
  shop: ["shop"] as const,
  dungeons: (playerId: string) => ["dungeons", playerId] as const,
  dungeonRun: (playerId: string) => ["dungeonRun", playerId] as const,
  leaderboard: ["rating", "top"] as const,
  rank: (playerId: string) => ["rating", playerId] as const,
  power: (playerId: string) => ["power", playerId] as const,
  village: (playerId: string) => ["village", playerId] as const,
  heroes: (playerId: string) => ["heroes", playerId] as const,
  battleReports: (playerId: string) => ["battleReports", playerId] as const,
  wallet: (playerId: string) => ["wallet", playerId] as const,
  garrison: (playerId: string) => ["garrison", playerId] as const,
  quests: (playerId: string) => ["quests", playerId] as const,
  serverQuests: (playerId: string) => ["serverQuests", playerId] as const,
  marches: (playerId: string) => ["marches", playerId] as const,
  tutorial: (playerId: string) => ["tutorial", playerId] as const,
  inventory: (playerId: string) => ["inventory", playerId] as const,
  /** Історія каналу; для приватного — з конкретним співрозмовником. */
  chatHistory: (playerId: string, channel: string, partnerId: string | null) =>
    ["chat", playerId, channel, partnerId] as const,
  chatConversations: (playerId: string) => ["chat", playerId, "conversations"] as const,
  settings: (playerId: string) => ["settings", playerId] as const,
  /** Скринька й бейдж — спільний префікс: нове в скриньці інвалідує обидва. */
  mailbox: (playerId: string) => ["mail", playerId, "box"] as const,
  mailUnread: (playerId: string) => ["mail", playerId, "unread"] as const,
  /** Ринок гравця — префікс: мутації ринку інвалідують разом і «мої лоти», і вітрину, і котирування. */
  market: (playerId: string) => ["market", playerId] as const,
  marketBrowse: (playerId: string, kind: number | null, page: number) => ["market", playerId, "browse", kind, page] as const,
  marketQuote: (playerId: string, goods: unknown) => ["market", playerId, "quote", goods] as const,
  banners: (playerId: string) => ["banners", playerId] as const,
  clan: (playerId: string) => ["clan", playerId] as const,
  clanHelp: (playerId: string) => ["clanHelp", playerId] as const,
  /** Заявки до мого клану й запрошення мені — один префікс: обидва списки міняє та сама подія. */
  clanRequests: (playerId: string) => ["clanRequests", playerId] as const,
  clanApplications: (playerId: string) => ["clanRequests", playerId, "applications"] as const,
  clanInvites: (playerId: string) => ["clanRequests", playerId, "invites"] as const,
  /** Каталог кланів нікому не належить: ключ — від пошуку й сторінки. */
  clanBrowse: (search: string, page: number) => ["clans", "browse", search, page] as const,
  clanProfile: (clanId: string) => ["clans", "profile", clanId] as const,
  /** Ділянка мапи не належить гравцю: ключ — від центру й радіуса. */
  mapArea: (x: number, y: number, radius: number) => ["map", "area", x, y, radius] as const,
  mapCell: (x: number, y: number) => ["map", "cell", x, y] as const,
};
