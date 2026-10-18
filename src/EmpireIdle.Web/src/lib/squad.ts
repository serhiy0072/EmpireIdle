import type { HeroSummary } from "./queries/heroes";

/** Порожній загін заданого розміру. */
export function emptySquad(size: number): (string | null)[] {
  return Array.from({ length: size }, () => null);
}

/**
 * Збережений загін, очищений від героїв, яких більше немає серед доступних, і підрізаний під розмір.
 * Сховище може бути недоступне (приватне вікно) — тоді загін просто порожній.
 */
export function loadSquad(storageKey: string, size: number, available: HeroSummary[]): (string | null)[] {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey) ?? "[]") as unknown;
    const ids = new Set(available.map((hero) => hero.id));

    if (!Array.isArray(saved)) return emptySquad(size);

    return emptySquad(size).map((_, lane) => {
      const id: unknown = saved[lane];
      return typeof id === "string" && ids.has(id) ? id : null;
    });
  } catch {
    return emptySquad(size);
  }
}

export function saveSquad(storageKey: string, lanes: (string | null)[]): void {
  try {
    localStorage.setItem(storageKey, JSON.stringify(lanes));
  } catch {
    // Без сховища загін просто не запам'ятається — бій від цього не залежить
  }
}
