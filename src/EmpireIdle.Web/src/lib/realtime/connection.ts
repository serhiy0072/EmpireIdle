import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";
import type { QueryClient } from "@tanstack/react-query";
import { API_URL, freshAccessToken } from "../api";
import { queryKeys } from "../queryKeys";
import { gameEventNames, type GameEventName, type GameEvents } from "./events";

type Handler<TName extends GameEventName> = (payload: GameEvents[TName]) => void;

const listeners = new Map<GameEventName, Set<(payload: never) => void>>();

/** Підписка для UI: тости, анімації, звуки. Кеш оновлюється окремо й завжди. */
export function onGameEvent<TName extends GameEventName>(name: TName, handler: Handler<TName>): () => void {
  const set = listeners.get(name) ?? new Set<(payload: never) => void>();

  set.add(handler as (payload: never) => void);
  listeners.set(name, set);

  return () => {
    set.delete(handler as (payload: never) => void);
  };
}

function emit<TName extends GameEventName>(name: TName, payload: GameEvents[TName]): void {
  listeners.get(name)?.forEach((handler) => (handler as Handler<TName>)(payload));
}

/** Що інвалідувати після кожної події. Точкові патчі кешу додамо разом з екранами. */
function invalidate(name: GameEventName, queryClient: QueryClient, playerId: string): void {
  const keys: Record<GameEventName, readonly (readonly unknown[])[]> = {
    // Збір, апгрейд і бій рухають цілі квестів
    BuildingCollected: [queryKeys.village(playerId), queryKeys.quests(playerId)],
    UpgradeStarted: [queryKeys.village(playerId)],
    UpgradeCompleted: [queryKeys.village(playerId), queryKeys.quests(playerId)],
    // Бій ранить героїв і юнітів гарнізону
    // Після бою марш розвертається, монстр міг зникнути з мапи
    BattleFinished: [
      queryKeys.battleReports(playerId),
      queryKeys.heroes(playerId),
      queryKeys.garrison(playerId),
      queryKeys.quests(playerId),
      queryKeys.marches(playerId),
      queryKeys.power(playerId),
      queryKeys.incoming(playerId),
      ["map"],
    ],
    MarchReturned: [
      queryKeys.marches(playerId),
      queryKeys.garrison(playerId),
      queryKeys.village(playerId),
      queryKeys.heroes(playerId),
      queryKeys.power(playerId),
    ],
    ServerQuestRewarded: [queryKeys.wallet(playerId), queryKeys.serverQuests(playerId)],
    ClanInvite: [queryKeys.clanRequests(playerId), ["mail", playerId]],
    // Подія лише підсвічує: зміст скриньки перечитується запитом. Лист про
    // падіння означає, що село вже на іншій клітині, — тож і село, і мапу
    MailReceived: [["mail", playerId], queryKeys.village(playerId), ["map"]],
    // Історію каналу чат дописує сам із події; список розмов — перечитуємо
    ChatMessage: [queryKeys.chatConversations(playerId)],
    // Новий ворожий марш — на мапу; зруйнована споруда зникає з мапи й звільняє слот
    AttackIncoming: [queryKeys.incoming(playerId)],
    AttackCalledOff: [queryKeys.incoming(playerId)],
    // Розвідники звітують на місці й назад не йдуть — марш зникає разом зі звітом
    ScoutReportReady: [queryKeys.scoutReports(playerId), queryKeys.marches(playerId)],
    StructureDestroyed: [queryKeys.territory(playerId), queryKeys.incoming(playerId), ["map"]],
  };

  keys[name].forEach((key) => void queryClient.invalidateQueries({ queryKey: key }));
}

export function startRealtime(queryClient: QueryClient, playerId: string): () => void {
  const connection: HubConnection = new HubConnectionBuilder()
    // Фабрика викликається на кожному підключенні й повертає токен,
    // що проживе ще пів хвилини: протухлий ротується до negotiate
    .withUrl(`${API_URL}/hubs/game`, { accessTokenFactory: async () => (await freshAccessToken()) ?? "" })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  gameEventNames.forEach((name) => {
    connection.on(name, (payload: GameEvents[typeof name]) => {
      invalidate(name, queryClient, playerId);
      emit(name, payload);
    });
  });

  // Поки зв'язку не було, події губилися: усе на екрані вже могло застаріти
  connection.onreconnected(() => void queryClient.invalidateQueries());

  // withAutomaticReconnect лікує лише обриви живого з'єднання,
  // невдалий перший старт SignalR не повторює — робимо це самі
  const delays = [2_000, 5_000, 10_000, 30_000];
  let stopped = false;

  const start = async (attempt: number): Promise<void> => {
    if (stopped) return;

    try {
      await connection.start();
    } catch (error: unknown) {
      if (stopped) return;

      console.warn("SignalR: підключення не вдалося, повторюємо", error);
      await new Promise((resolve) => window.setTimeout(resolve, delays[Math.min(attempt, delays.length - 1)]));

      return start(attempt + 1);
    }
  };

  const starting = start(0);

  // StrictMode монтує ефект двічі: зупиняємо лише після того, як спроба завершилась
  return () => {
    stopped = true;
    void starting.then(() => connection.stop());
  };
}
