/** Назви статів героїв, спорядження й наборів. Ключі — як у конфігу сервера. */
const STAT_LABELS: Record<string, string> = {
  Attack: "Атака",
  Defense: "Захист",
  Health: "Здоров'я",
  UnitAttack: "Атака юнітів",
  UnitDefense: "Захист юнітів",
  CritChance: "Шанс криту",
  CritPower: "Сила криту",
  SkillDamage: "Шкода вмінь",
  AttackSpeed: "Швидкість атаки",
  CooldownReduction: "Зменшення кулдауну",
  Lifesteal: "Вампіризм",
  BlockChance: "Шанс блоку",
  BlockPower: "Сила блоку",
  DamageReduction: "Зниження шкоди",
  AttackPercent: "Атака героя",
  DefensePercent: "Захист героя",
  HealthPercent: "Здоров'я героя",
  UnitCritChance: "Шанс криту юнітів",
  UnitHealthPercent: "Здоров'я юнітів",
};

/** Пласкі стати артефакта — числом; решта бонусів заточки — відсотками (GDD §9.12). */
const FLAT_STATS = new Set(["Attack", "Defense", "Health", "UnitAttack", "UnitDefense"]);

/** Невідомий стат повертається ключем: краще сирий рядок, ніж порожнє місце. */
export function statLabel(key: string): string {
  return STAT_LABELS[key] ?? key;
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
  return FLAT_STATS.has(key) ? Math.round(value).toLocaleString("uk-UA") : `${Number(value.toFixed(1)).toLocaleString("uk-UA")}%`;
}
