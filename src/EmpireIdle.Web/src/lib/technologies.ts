import { t } from "i18next";
import uk from "../locales/uk/technologies.json";
import type { TechnologyStat } from "./apiTypes";

type NodeKey = keyof typeof uk.node;
type StatKey = keyof typeof uk.stat;

/**
 * Назва вузла технологій мовою гравця. Структура дерева живе в конфігу сервера, а назви — на клієнті:
 * у конфігу технологій немає назв, лише ключі. Невідомий ключ повертається як є, щоб не показати порожнє місце.
 */
export function technologyName(key: string): string {
  return key in uk.node ? t(`technologies:node.${key as NodeKey}`) : key;
}

/** Імена TechnologyStat у порядку enum на сервері: на дроті — число. */
const STATS: readonly StatKey[] = [
  "Attack",
  "Defense",
  "ConvoySize",
  "MarchSpeed",
  "Carry",
  "Production",
  "MealUpkeep",
  "ConstructionSpeed",
  "TrainingSpeed",
  "UnitLevelUpSpeed",
  "ResearchSpeed",
  "WoundedCapacity",
  "UnitCapacity",
  "HeroExperience",
  "TameChance",
  "ProtectedStorage",
  "HelpShareCap",
  "OfferingAttempts",
  "CoinBonus",
  "TerritoryAttack",
  "TerritoryDefense",
  "StructureBuildSpeed",
  "StructureGarrison",
  "StructureCost",
  "LootCashback",
];

/** Стати, що додають штуки, а не частку: «+5», а не «+5%». */
const FLAT: ReadonlySet<StatKey> = new Set(["ConvoySize", "HelpShareCap", "OfferingAttempts"]);

export interface TechnologyEffect {
  stat: TechnologyStat;
  target?: string | null;
}

/** Ціль ефекту — юніт або ресурс; назву дає каталог. */
export interface TargetNames {
  unitName: (key: string) => string;
  resourceName: (key: string) => string;
}

/** «Атака · Піхота» або просто «Швидкість маршу», коли ефект на всіх. */
export function effectLabel(effect: TechnologyEffect, names: TargetNames): string {
  const stat = STATS[effect.stat];
  const label = stat === undefined ? String(effect.stat) : t(`technologies:stat.${stat}`);

  if (effect.target == null) return label;

  const target = stat === "Production" ? names.resourceName(effect.target) : names.unitName(effect.target);

  return `${label} · ${target}`;
}

/** Значення ефекту зі знаком: частки — у відсотках, штуки — як є. */
export function effectValue(stat: TechnologyStat, value: number): string {
  const key = STATS[stat];

  if (key !== undefined && FLAT.has(key)) return `+${Math.round(value)}`;

  return `+${Math.round(value * 1000) / 10}%`;
}

/** Тір римськими цифрами, як на референсі: «Натиск піхоти II». */
export function tierNumeral(tier: number): string {
  const numerals: [number, string][] = [
    [10, "X"],
    [9, "IX"],
    [5, "V"],
    [4, "IV"],
    [1, "I"],
  ];
  let rest = tier;
  let result = "";

  for (const [value, numeral] of numerals) {
    while (rest >= value) {
      result += numeral;
      rest -= value;
    }
  }

  return result;
}
