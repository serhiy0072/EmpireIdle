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
  const raw = localStorage.getItem(KEY) ?? sessionStorage.getItem(KEY);

  if (raw === null) return null;

  try {
    return JSON.parse(raw) as Session;
  } catch {
    // Зіпсований запис не має ламати старт застосунку
    clear();
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

/** remember не передають при ротації: тоді сесія лишається там, де вже лежала. */
export function setSession(session: Session | null, remember?: boolean): void {
  current = session;

  if (session === null) {
    clear();
  } else {
    const target =
      remember === undefined
        ? localStorage.getItem(KEY) !== null
          ? localStorage
          : sessionStorage
        : store(remember);

    clear();
    target.setItem(KEY, JSON.stringify(session));
  }

  listeners.forEach((listener) => listener());
}

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
