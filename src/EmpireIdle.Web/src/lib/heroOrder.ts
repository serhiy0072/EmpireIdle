import type { HeroSummary } from "./queries/heroes";

const RANK_ORDER: Record<string, number> = { Unique: 0, Rare: 1, Common: 2 };

/** Найсильніші першими: сила, далі рідкість і рівень — ростер не стрибає між однаковими героями. */
export function sortRoster(heroes: HeroSummary[], rankOf: (heroKey: string) => string | undefined): HeroSummary[] {
  return [...heroes].sort(
    (a, b) =>
      b.power - a.power ||
      (RANK_ORDER[rankOf(a.heroKey) ?? ""] ?? 9) - (RANK_ORDER[rankOf(b.heroKey) ?? ""] ?? 9) ||
      b.effectiveLevel - a.effectiveLevel ||
      a.id.localeCompare(b.id),
  );
}
