/**
 * Контракт подій сервера. Дзеркалить realtime/events.json у корені репозиторію —
 * той файл генерується з IGameClient і звіряється тестом на беку.
 * Якщо тест упав, правити треба обидва боки.
 */

export interface BuildingCollectedEvent {
  buildingId: string;
  resourceType: string;
  collected: number;
  /** Баланс після зарахування: село перезапитувати не треба. */
  newVillageAmount: number;
}

export interface UpgradeStartedEvent {
  buildingId: string;
  /** UTC. Таймер рахуємо від нього, а не від годинника клієнта. */
  completesAt: string;
}

export interface UpgradeCompletedEvent {
  buildingId: string;
  newLevel: number;
}

export interface BattleFinishedEvent {
  reportId: string;
  won: boolean;
  targetName: string;
}

/** Армія вдома: юніти в гарнізоні, здобич на складі. */
export interface MarchReturnedEvent {
  marchId: string;
}

export interface ServerQuestRewardedEvent {
  questKey: string;
  rank: number;
  contribution: number;
}

export interface ClanInviteEvent {
  requestId: string;
  clanId: string;
  clanName: string;
  clanTag: string;
  expiresAt: string;
}

/** Нове повідомлення чату. Переклади — на всі мови світу; клієнт бере свою. */
export interface ChatMessageEvent {
  id: string;
  /** "Server", "Clan" або "Private". */
  channel: string;
  senderId: string;
  senderName: string;
  clanId: string | null;
  recipientId: string | null;
  text: string;
  language: string;
  translations: Record<string, string>;
  sentAt: string;
}

/** Нове в скриньці: лист (kind — тип листа) або оголошення світу (kind = "Announcement"). */
export interface MailReceivedEvent {
  kind: string;
}

export interface GameEvents {
  BuildingCollected: BuildingCollectedEvent;
  UpgradeStarted: UpgradeStartedEvent;
  UpgradeCompleted: UpgradeCompletedEvent;
  BattleFinished: BattleFinishedEvent;
  MarchReturned: MarchReturnedEvent;
  ServerQuestRewarded: ServerQuestRewardedEvent;
  ClanInvite: ClanInviteEvent;
  ChatMessage: ChatMessageEvent;
  MailReceived: MailReceivedEvent;
}

export type GameEventName = keyof GameEvents;

export const gameEventNames: GameEventName[] = [
  "BuildingCollected",
  "UpgradeStarted",
  "UpgradeCompleted",
  "BattleFinished",
  "MarchReturned",
  "ServerQuestRewarded",
  "ClanInvite",
  "ChatMessage",
  "MailReceived",
];
