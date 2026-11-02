import type { HeroSummary } from "./queries/heroes";

const RANK_ORDER: Record<string, number> = { Unique: 0, Rare: 1, Common: 2 };

/** За чим сортувати ростер — перемикач над сіткою героїв. */
export type RosterSort = "power" | "tier" | "level" | "rarity";

export const ROSTER_SORTS: RosterSort[] = ["power", "tier", "level", "rarity"];

/** Значення з адреси сторінки; невідоме — сортування за силою. */
export function parseRosterSort(value: string | null): RosterSort {
  return ROSTER_SORTS.some((sort) => sort === value) ? (value as RosterSort) : "power";
}

/**
 * Найкращі першими за обраною ознакою; нічия розв'язується силою, рідкістю й рівнем,
 * а в кінці — id, тож ростер не стрибає між однаковими героями.
 */
export function sortRoster(
  heroes: HeroSummary[],
  rankOf: (heroKey: string) => string | undefined,
  by: RosterSort = "power",
): HeroSummary[] {
  const rank = (hero: HeroSummary) => RANK_ORDER[rankOf(hero.heroKey) ?? ""] ?? 9;
  const power = (a: HeroSummary, b: HeroSummary) => b.power - a.power;
  const rarity = (a: HeroSummary, b: HeroSummary) => rank(a) - rank(b);
  const level = (a: HeroSummary, b: HeroSummary) => b.effectiveLevel - a.effectiveLevel;
  const tier = (a: HeroSummary, b: HeroSummary) => b.tier - a.tier;

  const primary = { power, tier, level, rarity }[by];

  return [...heroes].sort(
    (a, b) => primary(a, b) || power(a, b) || rarity(a, b) || level(a, b) || a.id.localeCompare(b.id),
  );
}
