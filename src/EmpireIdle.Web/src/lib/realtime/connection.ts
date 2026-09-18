import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";
import type { QueryClient } from "@tanstack/react-query";
import { API_URL } from "../api";
import { queryKeys } from "../queryKeys";
import { getSession } from "../session";
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
    BuildingCollected: [queryKeys.village(playerId), queryKeys.buildings(playerId)],
    UpgradeStarted: [queryKeys.buildings(playerId)],
    UpgradeCompleted: [queryKeys.buildings(playerId), queryKeys.village(playerId)],
    BattleFinished: [queryKeys.battleReports(playerId), queryKeys.heroes(playerId)],
    ServerQuestRewarded: [queryKeys.wallet(playerId)],
    ClanInvite: [],
  };

  keys[name].forEach((key) => void queryClient.invalidateQueries({ queryKey: key }));
}

export function startRealtime(queryClient: QueryClient, playerId: string): () => void {
  const connection: HubConnection = new HubConnectionBuilder()
    // Фабрика викликається на кожному підключенні, тож після ротації
    // перепідключення бере вже новий токен
    .withUrl(`${API_URL}/hubs/game`, { accessTokenFactory: () => getSession()?.accessToken ?? "" })
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

  const starting = connection.start().catch((error: unknown) => {
    console.error("SignalR: не вдалося підключитись", error);
  });

  // StrictMode монтує ефект двічі: зупиняємо лише після того, як старт завершився
  return () => void starting.then(() => connection.stop());
}
