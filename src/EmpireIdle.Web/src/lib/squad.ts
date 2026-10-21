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

/**
 * Лінія класу, як у RealtimeBattle.HeroLines на сервері: воїни лоб у лоб, за ними лицарі, далі стрільці.
 * Тут вона лише впорядковує автовибір — у бою лінію ставить сервер.
 */
const LINE_BY_CLASS: Record<string, number> = { warrior: 0, knight: 1, archer: 2 };

/**
 * Найсильніші герої, розставлені по лініях: фронт займає центральні доріжки, далі — сусідні,
 * тил — крайні. Так фронт прикриває якнайбільше доріжок, а стрільці стоять осторонь.
 */
export function strongestSquad(heroes: HeroSummary[], size: number, classOf: (hero: HeroSummary) => string | undefined): (string | null)[] {
  const center = (size - 1) / 2;
  const lanes = Array.from({ length: size }, (_, lane) => lane).sort((a, b) => Math.abs(a - center) - Math.abs(b - center) || a - b);
  const line = (hero: HeroSummary) => LINE_BY_CLASS[classOf(hero) ?? ""] ?? Object.keys(LINE_BY_CLASS).length;

  const chosen = [...heroes]
    .sort((a, b) => b.power - a.power)
    .slice(0, size)
    .sort((a, b) => line(a) - line(b) || b.power - a.power);

  const squad = emptySquad(size);
  chosen.forEach((hero, i) => {
    const lane = lanes[i];
    if (lane !== undefined) squad[lane] = hero.id;
  });

  return squad;
}
