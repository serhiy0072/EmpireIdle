import { formatNumber } from "./format";
import { t } from "i18next";
/** Назви статів героїв, спорядження й наборів. Ключі — як у конфігу сервера. */
const STATS = [
  "Attack",
  "Defense",
  "Health",
  "UnitAttack",
  "UnitDefense",
  "CritChance",
  "CritPower",
  "SkillDamage",
  "AttackSpeed",
  "CooldownReduction",
  "Ward",
  "Lifesteal",
  "BlockChance",
  "BlockPower",
  "DamageReduction",
  "AttackPercent",
  "DefensePercent",
  "HealthPercent",
  "TroopsAttack",
  "TroopsDefense",
  "CooldownTurns",
] as const;

function isStat(key: string): key is (typeof STATS)[number] {
  return (STATS as readonly string[]).includes(key);
}

/** Юніти класу в родовому відмінку — для «Атака піхоти» замість безликого «військ класу». */
const CLASS_TROOPS = ["warrior", "knight", "archer"] as const;

function hasTroops(heroClass: string): heroClass is (typeof CLASS_TROOPS)[number] {
  return (CLASS_TROOPS as readonly string[]).includes(heroClass);
}

/** Стати, що діють лише в бою данжу: армійська формула їх не знає. Крит — ні: він входить у силу героя. */
const DUNGEON_ONLY = new Set([
  "SkillDamage",
  "Lifesteal",
  "DamageReduction",
  "BlockChance",
  "BlockPower",
  "CooldownTurns",
  "AttackSpeed",
  "CooldownReduction",
]);

/** Підказки до незвичних статів — при наведенні. */
const STAT_HINTS = [
  "CritChance",
  "CritPower",
  "Ward",
  "BlockChance",
  "BlockPower",
  "Lifesteal",
  "TroopsAttack",
  "TroopsDefense",
  "CooldownTurns",
  "AttackSpeed",
  "CooldownReduction",
] as const;

function hasHint(key: string): key is (typeof STAT_HINTS)[number] {
  return (STAT_HINTS as readonly string[]).includes(key);
}

/** Пласкі стати артефакта — числом; решта бонусів заточки — відсотками (GDD §9.12). */
const FLAT_STATS = new Set(["Attack", "Defense", "Health", "UnitAttack", "UnitDefense", "CooldownTurns"]);

/** Невідомий стат повертається ключем: краще сирий рядок, ніж порожнє місце. */
export function statLabel(key: string): string {
  return isStat(key) ? t(`common:stat.${key}`) : key;
}

/** Назва стата з класом набору: «Атака піхоти» для воїнського набору. */
export function statLabelForClass(key: string, heroClass: string | null | undefined): string {
  if (heroClass != null && hasTroops(heroClass)) {
    if (key === "TroopsAttack") return t(`common:classTroops.attack.${heroClass}`);
    if (key === "TroopsDefense") return t(`common:classTroops.defense.${heroClass}`);
  }

  return statLabel(key);
}

/** Чи діє стат лише в данжі — тоді картка дописує «у данжі». */
export function isDungeonOnly(key: string): boolean {
  return DUNGEON_ONLY.has(key);
}

/** Пояснення стата для підказки; порожньо — стат зрозумілий з назви. */
export function statHint(key: string): string | undefined {
  return hasHint(key) ? t(`common:statHint.${key}`) : undefined;
}

/** Бонус набору для показу: «+9%», «+120», а перезарядка — «−1 хід». */
export function bonusValue(key: string, value: number): string {
  if (key === "CooldownTurns") return t("common:cooldownTurns", { count: value });

  return `+${statValue(key, value)}`;
}

/**
 * Що дасть наступний рівень: лише стати, що виросли, — «Атака +12». Порожньо, якщо рівень на стелі.
 */
export function statGains(now: Record<string, number>, next: Record<string, number> | null | undefined): string[] {
  if (next == null) return [];

  return Object.entries(next)
    .map(([stat, value]) => [stat, value - (now[stat] ?? 0)] as const)
    .filter(([, gain]) => gain > 0.05)
    .map(([stat, gain]) => `${statLabel(stat)} +${statValue(stat, gain)}`);
}

/** Значення стату для показу: «120» для пласких, «3%» для відсоткових. */
export function statValue(key: string, value: number): string {
  return FLAT_STATS.has(key) ? formatNumber(Math.round(value)) : `${formatNumber(Number(value.toFixed(1)))}%`;
}
