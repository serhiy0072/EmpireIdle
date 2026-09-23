import type { components } from "./schema";

export type AuthResponse = components["schemas"]["AuthResponse"];

export interface Session {
  accessToken: string;
  refreshToken: string;
  playerId: string;
}

const KEY = "empireidle.session";

let current: Session | null = read();
const listeners = new Set<() => void>();

/** "Запам'ятати мене" — це вибір сховища: localStorage переживає вкладку, sessionStorage ні. */
function store(remember: boolean): Storage {
  return remember ? localStorage : sessionStorage;
}

function read(): Session | null {
  // Спершу своє sessionStorage: вкладка, увійшла без «Запам'ятати мене», після
  // перезавантаження не має підхопити акаунт зі спільного localStorage
  const raw = sessionStorage.getItem(KEY) ?? localStorage.getItem(KEY);
  const session = parse(raw);

  // Зіпсований запис не має ламати старт застосунку
  if (raw !== null && session === null) clear();

  return session;
}

function parse(raw: string | null): Session | null {
  if (raw === null) return null;

  try {
    return JSON.parse(raw) as Session;
  } catch {
    return null;
  }
}

function clear(): void {
  localStorage.removeItem(KEY);
  sessionStorage.removeItem(KEY);
}

export function getSession(): Session | null {
  return current;
}

function notify(): void {
  listeners.forEach((listener) => listener());
}

/** remember не передають при ротації: тоді сесія лишається там, де вже лежала. */
export function setSession(session: Session | null, remember?: boolean): void {
  const previous = current;
  current = session;

  // У спільному localStorage може лежати сесія іншого акаунта з сусідньої
  // вкладки: чіпаємо лише записи свого гравця
  const owns = (storage: Storage, playerId: string | undefined) =>
    playerId === undefined || parse(storage.getItem(KEY))?.playerId === playerId;

  if (session === null) {
    for (const storage of [localStorage, sessionStorage])
      if (owns(storage, previous?.playerId)) storage.removeItem(KEY);
  } else {
    // Ротація лишає сесію там, де лежала сесія цього гравця
    const target =
      remember === undefined
        ? parse(localStorage.getItem(KEY))?.playerId === session.playerId
          ? localStorage
          : sessionStorage
        : store(remember);

    const other = target === localStorage ? sessionStorage : localStorage;

    // Лише копію з іншого сховища: видалення з цільового сусідня вкладка
    // побачила б як вихід і розлогінилась би посеред ротації
    if (owns(other, session.playerId)) other.removeItem(KEY);
    target.setItem(KEY, JSON.stringify(session));
  }

  notify();
}

/**
 * Підтягує сесію того самого гравця, яку записала інша вкладка: сховище в
 * них спільне, а пам'ять — ні. Без цього вкладка пред'явила б уже використаний
 * refresh-токен, і сервер відкликав би всі сесії гравця. Сесію іншого акаунта
 * ігноруємо — вкладка не має перескакувати на чужого гравця.
 */
export function syncSessionFromStorage(): Session | null {
  const stored = read();

  if (current !== null && stored !== null && stored.playerId === current.playerId && stored.refreshToken !== current.refreshToken) {
    current = stored;
    notify();
  }

  return current;
}

// Подія storage приходить лише з інших вкладок і лише для localStorage
window.addEventListener("storage", (event) => {
  if (event.key !== KEY || event.storageArea !== localStorage || current === null) return;

  if (event.newValue !== null) {
    syncSessionFromStorage();
    return;
  }

  // Той самий гравець вийшов в іншій вкладці — його refresh-токен уже мертвий і тут
  if (parse(event.oldValue)?.playerId === current.playerId) {
    current = null;
    notify();
  }
});

export function fromAuthResponse(response: AuthResponse): Session {
  return {
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    playerId: response.playerId,
  };
}

/** Для useSyncExternalStore: сесію читає й перезаписує ще й клієнт API під час ротації. */
export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
