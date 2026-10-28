import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { api } from "../api";
import { queryKeys } from "../queryKeys";
import type { components } from "../schema";
import { useLanguage } from "../i18n";
import { t } from "i18next";

export type CatalogResponse = components["schemas"]["CatalogResponse"];
export type CatalogHero = components["schemas"]["CatalogHero"];
export type CatalogItem = components["schemas"]["CatalogItem"];
export type CatalogResource = components["schemas"]["CatalogResource"];
export type CatalogBuilding = components["schemas"]["CatalogBuilding"];

/** Таймер прискорення — ключ межі в каталозі (SpeedUpTimer на сервері). */
export type SpeedUpTimer = "Construction" | "Training" | "UnitLevelUp" | "March";
export type CatalogSkill = components["schemas"]["CatalogSkill"];
export type CatalogUnit = components["schemas"]["CatalogUnit"];
export type CatalogBeast = components["schemas"]["CatalogBeast"];
export type CatalogArtifactSet = components["schemas"]["CatalogArtifactSet"];
export type CatalogSetRarity = components["schemas"]["CatalogSetRarity"];
export type CatalogArtifactSlot = components["schemas"]["CatalogArtifactSlot"];
export type CatalogLootbox = components["schemas"]["CatalogLootbox"];
export type CatalogArtifactRepair = components["schemas"]["CatalogArtifactRepair"];
export type CatalogMasteryAttempt = components["schemas"]["CatalogMasteryAttempt"];

export interface Catalog {
  /** false, поки довідник не приїхав: екрани показують ключі замість назв. */
  loaded: boolean;
  hero: (key: string) => CatalogHero | null;
  /** Увесь ростер гри — для кодексу. */
  allHeroes: CatalogHero[];
  item: (key: string) => CatalogItem | null;
  building: (key: string) => CatalogBuilding | null;
  unit: (key: string) => CatalogUnit | null;
  /** Усі типи юнітів — щоб порівняти показник із найкращим серед них. */
  units: CatalogUnit[];
  heroName: (key: string) => string;
  itemName: (key: string) => string;
  resourceName: (key: string) => string;
  /** Місце ресурсу в довіднику: за ним шапка тримає сталий порядок, хоч сервер його не гарантує. */
  resourceOrder: (key: string) => number;
  buildingName: (key: string) => string;
  unitName: (key: string) => string;
  beastName: (key: string) => string;
  /** Назва вміння за ключем — вміння живуть у героях, ключі унікальні на всю гру. */
  skillName: (key: string) => string;
  /** Назва родини наборів артефактів за її ключем («dawn»). */
  artifactSetName: (key: string) => string;
  /** Звір, якого дає монстр цього типу; null — монстр не приручається. */
  beastForMonster: (monsterType: string) => CatalogBeast | null;
  /** Юніти, які ця будівля вже може тренувати на своєму поточному рівні. */
  unitsFor: (buildingType: string, buildingLevel: number) => CatalogUnit[];
  /** Ключі будівель, що тренують хоч один тип юнітів. */
  trainingBuildingKeys: string[];
  /** Зірки героя (GDD §6.1): скільки їх і скільки частинок у кожній. */
  maxStars: number;
  partsPerStar: number;
  /** Стеля рівня вміння; доступний рівень — зірки + 1, не вище за неї (GDD §6.1). */
  maxSkillLevel: number;
  /** Скільки книг коштує підняти вміння з рівня n на n + 1 (індекс 0 — з першого); рішення 15.10.2026. */
  skillBookCosts: number[];
  /** Ключ предмета-книги для вмінь цієї половини в героя такої ролі й рідкості; null — книги немає. */
  skillBook: (heroClass: string, rarity: string, half: string) => string | null;
  /** Скільки осколків коштує призов будь-якого героя. */
  summonShards: number;
  maxTier: number;
  /** Кап рівня юніта від тренування чи прокачки (§5.2 GDD). */
  maxUnitLevel: number;
  /** На скільки кожен рівень юніта понад перший підсилює атаку, захист і здоров'я (0.1 — +10%). */
  unitLevelStatBonus: number;
  /** Скільки юнітів будівля тренує чи качає за одну партію на цьому рівні. */
  unitBatchSize: (buildingLevel: number) => number;
  /** Скільки юнітів своїх типів вміщує будівля юнітів цього рівня (рішення 13.10.2026). */
  unitCapacity: (buildingLevel: number) => number;
  /** Ціна лікування пораненого юніта в gems — та сама для будь-якого типу. */
  healGemsPerUnit: number;
  /** Частка ціни нового юніта, яку коштує лікування ресурсами. */
  healResourceCostShare: number;
  /** Скільки героїв веде один марш. */
  maxHeroesPerMarch: number;
  /** Артефактні слоти героя за типом (намисто, корона…) у порядку номерів. */
  artifactSlots: CatalogArtifactSlot[];
  /** Назва слота, у який вдягається артефакт; null — не артефакт. */
  artifactSlotName: (itemKey: string) => string | null;
  /** Стеля рівня артефакта (згодовування). */
  maxArtifactLevel: number;
  /** Стеля майстерності коваля. */
  maxMastery: number;
  /** Накопичений досвід, з яким артефакт стає рівня n (індекс — рівень). */
  artifactLevelExperience: number[];
  /** Ціна в золоті й шанс успіху спроби з кожної поточної майстерності (індекс 0 — спроба на +1). */
  masteryAttempts: CatalogMasteryAttempt[];
  /** Камінь для заточування — ним платять за спробу заточки (рішення 14.10.2026). */
  masteryStoneItemKey: string;
  /** Ціна ремонту й шанси поломки на кожну заточку. */
  artifactRepair: CatalogArtifactRepair;
  /** Сторона світової мапи в клітинах. */
  mapSize: number;
  /** Ключ головної будівлі — її рівень показуємо як рівень гравця. */
  mainBuildingKey: string;
  /** Родини наборів артефактів у порядку рівнів. */
  artifactSets: CatalogArtifactSet[];
  /** Родина, до якої належить предмет; null — предмет поза наборами. */
  setOfItem: (itemKey: string) => CatalogArtifactSet | null;
  /** Таблиця скрині з шансами й гарантією; null — предмет не скриня. */
  lootbox: (itemKey: string) => CatalogLootbox | null;
  /**
   * Ціна прискорення таймера на момент `now` — та сама формула, що в SpeedUpCalculator:
   * платиться все понад межу свого таймера (floorSeconds[timer], немає — нуль), щонайменше 1 gem.
   * 0 — лишилась межа, прискорювати нічого, кнопку не показуємо. Поки каталог не приїхав — ціна із запиту.
   */
  speedUpCost: (timer: SpeedUpTimer, completesAt: string, now: number, serverCost: number) => number;
  /** Межа таймера, нижче якої прискорення не зрізає (будівництво й тренування — 0, марш — 30 с). */
  speedUpFloorSeconds: (timer: SpeedUpTimer) => number;
  /** Варіанти прискорення маршу: частка залишку дороги за сталу ціну, від меншої. */
  marchSpeedUps: { percent: number; gems: number }[];
}

/**
 * Довідник гри. Тягнеться один раз: у межах запуску сервера він незмінний,
 * а після перезавантаження сторінки браузер отримає 304 за ETag.
 */
export function useCatalog(): Catalog {
  const language = useLanguage();

  const query = useQuery({
    queryKey: queryKeys.catalog(language),
    queryFn: () => api<CatalogResponse>(`/api/catalog?lang=${encodeURIComponent(language)}`),
    // Відкрита вкладка раз на 5 хвилин перевіряє версію — з ETag це 304 без тіла
    staleTime: 5 * 60_000,
  });

  return useMemo(() => {
    const data = query.data;

    const heroes = new Map((data?.heroes ?? []).map((hero) => [hero.key, hero]));
    const items = new Map((data?.items ?? []).map((item) => [item.key, item]));
    const resources = new Map((data?.resources ?? []).map((resource) => [resource.key, resource]));
    const resourceIndex = new Map((data?.resources ?? []).map((resource, index) => [resource.key, index]));
    const buildings = new Map((data?.buildings ?? []).map((building) => [building.key, building]));
    const units = new Map((data?.units ?? []).map((unit) => [unit.key, unit]));
    const beasts = new Map((data?.beasts ?? []).map((beast) => [beast.key, beast]));
    const skills = new Map((data?.heroes ?? []).flatMap((hero) => hero.skills.map((skill) => [skill.key, skill] as const)));
    const sets = new Map((data?.artifactSets ?? []).map((set) => [set.key, set]));
    const lootboxes = new Map((data?.lootboxes ?? []).map((box) => [box.key, box]));
    const beastsByMonster = new Map((data?.beasts ?? []).map((beast) => [beast.monsterKey, beast]));
    // SetKey предмета («dawn_rare») → родина («dawn»): так картка артефакту знає свій набір
    const familyBySetKey = new Map(
      (data?.artifactSets ?? []).flatMap((set) => set.rarities.map((rarity) => [rarity.setKey, set] as const)),
    );

    return {
      loaded: data !== undefined,
      hero: (key) => heroes.get(key) ?? null,
      allHeroes: data?.heroes ?? [],
      item: (key) => items.get(key) ?? null,
      building: (key) => buildings.get(key) ?? null,
      unit: (key) => units.get(key) ?? null,
      units: data?.units ?? [],
      // Ключ як запасна назва: краще технічний рядок, ніж порожнє місце
      heroName: (key) => heroes.get(key)?.displayName ?? key,
      itemName: (key) => items.get(key)?.displayName ?? key,
      resourceName: (key) => resources.get(key)?.displayName ?? key,
      resourceOrder: (key) => resourceIndex.get(key) ?? Number.MAX_SAFE_INTEGER,
      buildingName: (key) => buildings.get(key)?.displayName ?? key,
      unitName: (key) => units.get(key)?.displayName ?? key,
      beastName: (key) => beasts.get(key)?.displayName ?? key,
      skillName: (key) => skills.get(key)?.displayName ?? key,
      artifactSetName: (key) => sets.get(key)?.displayName ?? key,
      beastForMonster: (monsterType) => beastsByMonster.get(monsterType) ?? null,
      unitsFor: (buildingType, buildingLevel) =>
        (data?.units ?? []).filter(
          (unit) => unit.requiresBuilding === buildingType && unit.requiresBuildingLevel <= buildingLevel,
        ),
      trainingBuildingKeys: [
        ...new Set((data?.units ?? []).flatMap((unit) => (unit.requiresBuilding == null ? [] : [unit.requiresBuilding]))),
      ],
      maxStars: data?.maxStars ?? 5,
      partsPerStar: data?.partsPerStar ?? 6,
      maxSkillLevel: data?.maxSkillLevel ?? 6,
      skillBookCosts: data?.skillBookCosts ?? [],
      skillBook: (heroClass, rarity, half) =>
        (data?.skillBooks ?? []).find(
          (book) =>
            book.class === heroClass && book.rarity.toLowerCase() === rarity.toLowerCase() && book.half === half,
        )?.itemKey ?? null,
      summonShards: data?.summonShards ?? 10,
      maxTier: data?.maxTier ?? 3,
      maxUnitLevel: data?.maxUnitLevel ?? 10,
      unitLevelStatBonus: data?.unitLevelStatBonus ?? 0.1,
      unitBatchSize: (buildingLevel) => {
        const sizes = data?.unitBatchSizes ?? [];
        return sizes[Math.min(Math.max(buildingLevel, 1), sizes.length) - 1] ?? 1;
      },
      unitCapacity: (buildingLevel) => {
        const capacities = data?.unitCapacities ?? [];
        return capacities[Math.min(Math.max(buildingLevel, 1), capacities.length) - 1] ?? 0;
      },
      healGemsPerUnit: data?.healGemsPerUnit ?? 1,
      healResourceCostShare: data?.healResourceCostShare ?? 0.5,
      maxHeroesPerMarch: data?.maxHeroesPerMarch ?? 3,
      artifactSlots: data?.artifactSlots ?? [],
      artifactSlotName: (itemKey) => {
        const slot = items.get(itemKey)?.artifactSlot;
        return slot == null ? null : (data?.artifactSlots.find((s) => s.key === slot)?.displayName ?? slot);
      },
      maxArtifactLevel: data?.maxArtifactLevel ?? 20,
      maxMastery: data?.maxMastery ?? 10,
      artifactLevelExperience: data?.artifactLevelExperience ?? [],
      masteryAttempts: data?.masteryAttempts ?? [],
      masteryStoneItemKey: data?.masteryStoneItemKey ?? "whetstone",
      artifactRepair: data?.artifactRepair ?? { gemsPerMastery: 0, kitItemKey: "repair_kit", breakChances: [] },
      mapSize: data?.mapSize ?? 500,
      mainBuildingKey: data?.mainBuildingKey ?? "townhall",
      artifactSets: data?.artifactSets ?? [],
      setOfItem: (itemKey) => {
        const setKey = items.get(itemKey)?.setKey;
        return setKey == null ? null : (familyBySetKey.get(setKey) ?? null);
      },
      lootbox: (itemKey) => lootboxes.get(itemKey) ?? null,
      speedUpFloorSeconds: (timer) => data?.speedUp.floorSeconds[timer] ?? 0,
      marchSpeedUps: data?.speedUp.marchOptions ?? [],
      speedUpCost: (timer, completesAt, now, serverCost) => {
        if (data === undefined) return serverCost;

        const { floorSeconds, factor, exponent } = data.speedUp;
        const floor = floorSeconds[timer] ?? 0;
        const minutes = (new Date(completesAt).getTime() - now - floor * 1_000) / 60_000;

        return minutes <= 0 ? 0 : Math.max(1, Math.ceil(factor * Math.pow(minutes, exponent)));
      },
    };
  }, [query.data]);
}

const RANK_STYLES: Record<string, string> = {
  Common: "bg-slate-100 text-slate-600",
  Rare: "bg-sky-100 text-sky-800",
  Unique: "bg-amber-100 text-amber-800",
};

const RANK_LABELS = ["Common", "Rare", "Unique"] as const;

function isRank(value: string): value is (typeof RANK_LABELS)[number] {
  return (RANK_LABELS as readonly string[]).includes(value);
}

export function rankStyle(rank: string | undefined): string {
  return RANK_STYLES[rank ?? ""] ?? "bg-slate-100 text-slate-600";
}

export function rankLabel(rank: string | undefined): string {
  return rank !== undefined && isRank(rank) ? t(`common:rank.${rank}`) : "—";
}

const STATES = ["Idle", "Deployed", "Wounded", "LevelingUp"] as const;

function isHeroState(value: string): value is (typeof STATES)[number] {
  return (STATES as readonly string[]).includes(value);
}

export function heroState(state: string): string {
  return isHeroState(state) ? t(`heroes:state.${state}`) : state;
}

const SKILL_KINDS = ["Active", "Passive", "Periodic", "Utility"] as const;

function isSkillKind(value: string): value is (typeof SKILL_KINDS)[number] {
  return (SKILL_KINDS as readonly string[]).includes(value);
}

/** Вид вміння словом для підпису. */
export function skillKindLabel(kind: string): string {
  return isSkillKind(kind) ? t(`heroes:skillKind.${kind}`) : kind;
}

/** Половина вмінь (GDD §6.1): її книга й піднімає вміння. */
export function skillHalfLabel(half: string): string {
  return half === "Attack" ? t("heroes:skillHalf.Attack") : half === "Defense" ? t("heroes:skillHalf.Defense") : half;
}

/** Значення з пер-рівневого списку — та сама формула, що HeroSkills.At на сервері. */
export function skillValue(values: number[], level: number): number {
  if (values.length === 0 || level < 1) return 0;

  return values[Math.min(level, values.length) - 1] ?? 0;
}

function skillStat(stat: string): string {
  return stat === "Attack" ? t("heroes:skillStat.Attack") : stat === "Defense" ? t("heroes:skillStat.Defense") : stat;
}

const SKILL_TARGETS = ["all", "infantry", "archer", "spearman"] as const;

function isSkillTarget(value: string): value is (typeof SKILL_TARGETS)[number] {
  return (SKILL_TARGETS as readonly string[]).includes(value);
}

function skillTarget(target: string): string {
  return isSkillTarget(target) ? t(`heroes:skillTarget.${target}`) : target;
}

/** Що вміння робить на рівні level; закрите (0) показується першим рівнем — «що відкриється». */
export function skillEffect(skill: CatalogSkill, level: number): string {
  const at = Math.max(1, level);
  const parts: string[] = [];

  if (skill.battle !== null && skill.battle !== undefined) {
    const scale = skillValue(skill.battle.levelScale, at);
    const power =
      skill.battle.damageMultiplier > 0
        ? t("heroes:skillEffect.damage", { value: (skill.battle.damageMultiplier * scale).toFixed(1) })
        : skill.battle.healPercent > 0
          ? t("heroes:skillEffect.heal", { value: Math.round(skill.battle.healPercent * scale * 100) })
          : skill.battle.shieldPercent > 0
            ? t("heroes:skillEffect.shield", { value: Math.round(skill.battle.shieldPercent * scale * 100) })
            : t("heroes:skillEffect.status");
    parts.push(t("heroes:skillEffect.dungeon", { power, cooldown: skill.battle.cooldown }));
  }

  if (skill.troops !== null && skill.troops !== undefined) {
    const percent = skillValue(skill.troops.percents, at);
    parts.push(
      t("heroes:skillEffect.troops", { percent, stat: skillStat(skill.troops.stat), target: skillTarget(skill.troops.target) }),
    );
  }

  if (skill.utility !== null && skill.utility !== undefined) {
    const percent = skillValue(skill.utility.percents, at);
    parts.push(
      skill.utility.effect === "MarchSpeed"
        ? t("heroes:skillEffect.marchSpeed", { percent })
        : `+${percent}% ${skill.utility.effect}`,
    );
  }

  return parts.join(" · ");
}
/** Рядок перегляду вміння за рівнями: що росте і значення на кожному рівні (рівень 1 — перше). */
export interface SkillPreviewRow {
  label: string;
  values: string[];
}

/**
 * Перегляд покращення, як у грі: «+2%/4%/6%…» по рівнях. Ті самі числа, що skillEffect,
 * лише всі рівні разом — гравець бачить, що дасть наступна книга.
 */
export function skillPreview(skill: CatalogSkill, maxLevel: number): SkillPreviewRow[] {
  const levels = Array.from({ length: maxLevel }, (_, index) => index + 1);
  const rows: SkillPreviewRow[] = [];

  if (skill.troops !== null && skill.troops !== undefined) {
    const troops = skill.troops;
    rows.push({
      label: `${skillStat(troops.stat)} ${skillTarget(troops.target)}`,
      values: levels.map((level) => `${skillValue(troops.percents, level)}%`),
    });
  }

  if (skill.utility !== null && skill.utility !== undefined) {
    const utility = skill.utility;
    rows.push({
      label: utility.effect === "MarchSpeed" ? t("heroes:skillPreview.marchSpeed") : utility.effect,
      values: levels.map((level) => `${skillValue(utility.percents, level)}%`),
    });
  }

  if (skill.battle !== null && skill.battle !== undefined && skill.battle.damageMultiplier > 0) {
    const battle = skill.battle;
    rows.push({
      label: t("heroes:skillPreview.dungeonDamage"),
      values: levels.map((level) => `×${(battle.damageMultiplier * skillValue(battle.levelScale, level)).toFixed(1)}`),
    });
  }

  return rows;
}
