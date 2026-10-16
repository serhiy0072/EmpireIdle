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
  CooldownReduction: "Скорочення перезарядки",
  Ward: "Оберіг",
  Lifesteal: "Вампіризм",
  BlockChance: "Шанс блоку",
  BlockPower: "Сила блоку",
  DamageReduction: "Зниження шкоди",
  AttackPercent: "Атака героя",
  DefensePercent: "Захист героя",
  HealthPercent: "Здоров'я героя",
  TroopsAttack: "Атака військ класу",
  TroopsDefense: "Захист військ класу",
  CooldownTurns: "Перезарядка вміння",
};

/** Юніти класу в родовому відмінку — для «Атака піхоти» замість безликого «військ класу». */
const CLASS_TROOPS: Record<string, string> = { warrior: "піхоти", knight: "списоносців", archer: "лучників" };

/** Стати, що діють лише в покроковому бою данжу: армійська формула їх не знає. */
const DUNGEON_ONLY = new Set([
  "CritChance",
  "CritPower",
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
const STAT_HINTS: Record<string, string> = {
  Ward: "Оберіг: у данжі — щит із частки здоров'я на перші ходи бою, в армії — герой витримує більше шкоди до поранення",
  BlockChance: "Шанс, що удар по герою в данжі буде заблоковано",
  BlockPower: "Яку частку шкоди гасить блок понад базові 30%",
  Lifesteal: "Частка завданої шкоди, що повертається герою здоров'ям",
  TroopsAttack: "Атака юнітів класу героя в марші й обороні",
  TroopsDefense: "Захист юнітів класу героя в марші й обороні",
  CooldownTurns: "На стільки ходів раніше знову готове активне вміння в данжі",
  AttackSpeed: "Більше ударів за секунду — запрацює з новим боєм данжів і арени",
  CooldownReduction: "Вміння готові частіше — запрацює з новим боєм данжів і арени",
};

/** Пласкі стати артефакта — числом; решта бонусів заточки — відсотками (GDD §9.12). */
const FLAT_STATS = new Set(["Attack", "Defense", "Health", "UnitAttack", "UnitDefense", "CooldownTurns"]);

/** Невідомий стат повертається ключем: краще сирий рядок, ніж порожнє місце. */
export function statLabel(key: string): string {
  return STAT_LABELS[key] ?? key;
}

/** Назва стата з класом набору: «Атака піхоти» для воїнського набору. */
export function statLabelForClass(key: string, heroClass: string | null | undefined): string {
  const troops = heroClass == null ? undefined : CLASS_TROOPS[heroClass];

  if (troops !== undefined && key === "TroopsAttack") return `Атака ${troops}`;
  if (troops !== undefined && key === "TroopsDefense") return `Захист ${troops}`;

  return statLabel(key);
}

/** Чи діє стат лише в данжі — тоді картка дописує «у данжі». */
export function isDungeonOnly(key: string): boolean {
  return DUNGEON_ONLY.has(key);
}

/** Пояснення стата для підказки; порожньо — стат зрозумілий з назви. */
export function statHint(key: string): string | undefined {
  return STAT_HINTS[key];
}

/** Бонус набору для показу: «+9%», «+120», а перезарядка — «−1 хід». */
export function bonusValue(key: string, value: number): string {
  if (key === "CooldownTurns") return `−${value} ${value === 1 ? "хід" : "ходи"}`;

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
  return FLAT_STATS.has(key) ? Math.round(value).toLocaleString("uk-UA") : `${Number(value.toFixed(1)).toLocaleString("uk-UA")}%`;
}
