import type {
  BattleReportResponse,
  ClanHelpItemResponse,
  GarrisonResponse,
  IncomingAttackResponse,
  InventoryResponse,
  MarchResponse,
  MyClanResponse,
  QuestView,
  VillageResponse,
  WalletResponse,
} from "../lib/apiTypes";
import type { BeastPenResponse } from "../lib/queries/beasts";
import type { Catalog } from "../lib/queries/catalog";
import type { HeroesOverview } from "../lib/queries/heroes";
import { t } from "i18next";
import { QUEST_STATE, QUEST_WINDOW } from "../lib/queries/quests";

/**
 * Зріз стану гри, з якого виводиться активний крок. Усе — з кешу запитів:
 * туторіал не має власного лічильника прогресу, лише множину побаченого.
 */
export interface TutorialSnapshot {
  catalog: Catalog;
  village: VillageResponse;
  garrison: GarrisonResponse | undefined;
  heroes: HeroesOverview | undefined;
  quests: QuestView[] | undefined;
  marches: MarchResponse[] | undefined;
  reports: BattleReportResponse[] | undefined;
  wallet: WalletResponse | undefined;
  inventory: InventoryResponse | undefined;
  incoming: IncomingAttackResponse[] | undefined;
  unreadMail: number;
  beastPen: BeastPenResponse | undefined;
  /** null — гравець поза кланом; undefined — ще не завантажено. */
  clan: MyClanResponse | null | undefined;
  clanHelp: ClanHelpItemResponse[] | undefined;
  /** Точний вказівник (миша): підказка про клавіші має сенс лише з ним. */
  pointerFine: boolean;
  /** Поточний екран (pathname): підказки «при першому вході» дивляться сюди. */
  route: string;
  /**
   * Щойно закрили контекстну підказку — наступна чекає зміни екрана або паузи
   * (рішення 22.10.2026): інакше на ратуші 3 висипалось би 5–6 карток поспіль.
   */
  hintsPaused: boolean;
  seen: ReadonlySet<string>;
  skipped: boolean;
}

/**
 * block — спотлайт, решта UI не клікабельна: лише кроки на кілька секунд.
 * guide — спотлайт і текст, UI вільний: гравець може чекати таймер чи збирати ресурси.
 * hint — картка внизу з «Зрозуміло», без підсвітки.
 */
export type StepMode = "block" | "guide" | "hint";

export interface TutorialStep {
  key: string;
  mode: StepMode;
  title: string;
  text: string;
  /**
   * Ідентифікатори data-tutorial у порядку пріоритету: спотлайт бере
   * перший, що є в DOM. Останнім зазвичай іде посилання навігації —
   * воно є на кожному екрані, тож гравця завжди є куди вести.
   */
  targets: string[];
  /** Крок має сенс показувати (передумови виконані). */
  available: (s: TutorialSnapshot) => boolean;
  /** Мета кроку досягнута в грі. Кроки без done закриваються кнопкою (seen). */
  done?: (s: TutorialSnapshot) => boolean;
}

const MAIN_BUILDING = "townhall";
/** Захист новачка діє, поки ратуша нижче цього рівня (Combat.NewbieShieldTownHallLevel). */
const NEWBIE_SHIELD_LEVEL = 3;
const GOLD_MINE = "goldmine";
const FIRST_UNIT = "infantry";

function building(s: TutorialSnapshot, type: string) {
  return s.village.buildings.find((b) => b.type === type);
}

function reachedOrBuilding(s: TutorialSnapshot, type: string, level: number): boolean {
  const b = building(s, type);

  return b !== undefined && (b.level >= level || (b.level >= level - 1 && b.isUnderConstruction));
}

function quest(s: TutorialSnapshot, key: string): QuestView | undefined {
  return s.quests?.find((q) => q.key === key);
}

function townHallLevel(s: TutorialSnapshot): number {
  return building(s, MAIN_BUILDING)?.level ?? 0;
}

function hasItem(s: TutorialSnapshot, test: (item: { itemKey: string; type: string }) => boolean): boolean {
  return s.inventory?.items.some((item) => item.count > 0 && test(item)) ?? false;
}

/** Будівля відкрита ратушею: для механік, що вмикаються будівлею, а не її рівнем. */
function unlocked(s: TutorialSnapshot, type: string): boolean {
  const config = s.catalog.building(type);

  return config !== null && townHallLevel(s) >= config.requiresMainBuildingLevel;
}

/** Два артефакти одного набору — перший момент, коли бонус набору досяжний. */
function hasSetPair(s: TutorialSnapshot): boolean {
  const counts = new Map<string, number>();

  for (const piece of s.inventory?.equipment ?? []) {
    const set = s.catalog.item(piece.itemKey)?.setKey;
    if (set == null) continue;

    const count = (counts.get(set) ?? 0) + 1;
    if (count >= 2) return true;
    counts.set(set, count);
  }

  return false;
}

function armySize(s: TutorialSnapshot): number {
  return (s.garrison?.units ?? []).reduce((sum, u) => sum + u.count, 0);
}

/** Ведений ланцюжок. Порядок у масиві — порядок показу. Функція, а не константа: тексти — мовою на момент показу. */
export function introSteps(): TutorialStep[] {
  return [
    {
      key: "intro.welcome",
      mode: "block",
      title: t("tutorial:intro.welcome.title"),
      text: t("tutorial:intro.welcome.text"),
      targets: [],
      available: () => true,
    },
    // Апгрейд першим: стартових ресурсів на нього вистачає, а шахта ще порожня —
    // блокувати гравця, поки вона накопичить 50 золота, означало б п'ять хвилин нудьги
    {
      key: "intro.upgrade",
      mode: "block",
      title: t("tutorial:intro.upgrade.title"),
      text: t("tutorial:intro.upgrade.text"),
      targets: ["upgrade", `building:${MAIN_BUILDING}`, "nav:/"],
      available: () => true,
      done: (s) => reachedOrBuilding(s, MAIN_BUILDING, 2),
    },
    {
      key: "intro.timers",
      mode: "hint",
      title: t("tutorial:intro.timers.title"),
      text: t("tutorial:intro.timers.text"),
      targets: [],
      available: (s) => building(s, MAIN_BUILDING)?.isUnderConstruction === true,
    },
    {
      key: "intro.collect",
      mode: "guide",
      title: t("tutorial:intro.collect.title"),
      text: t("tutorial:intro.collect.text"),
      targets: [`building:${GOLD_MINE}`, "nav:/"],
      available: () => true,
      done: (s) => {
        const q = quest(s, "intro_farm");
        return q !== undefined && q.state !== QUEST_STATE.inProgress;
      },
    },
    {
      key: "intro.claim",
      mode: "block",
      title: t("tutorial:intro.claim.title"),
      text: t("tutorial:intro.claim.text"),
      targets: ["claim:intro_farm", "nav:/quests"],
      available: (s) => quest(s, "intro_farm")?.state === QUEST_STATE.completed,
      done: (s) => quest(s, "intro_farm")?.state === QUEST_STATE.claimed,
    },
    {
      key: "intro.train",
      mode: "guide",
      title: t("tutorial:intro.train.title"),
      text: t("tutorial:intro.train.text"),
      targets: [`train:${FIRST_UNIT}`, "camp:barracks", "building:barracks", "nav:/"],
      available: () => true,
      done: (s) => armySize(s) >= 5 || (s.garrison?.trainingOrders.length ?? 0) > 0,
    },
    {
      key: "intro.townhall3",
      mode: "guide",
      title: t("tutorial:intro.townhall3.title"),
      text: t("tutorial:intro.townhall3.text"),
      targets: [`building:${MAIN_BUILDING}`, "nav:/"],
      available: (s) => reachedOrBuilding(s, MAIN_BUILDING, 2),
      done: (s) => reachedOrBuilding(s, MAIN_BUILDING, 3),
    },
    // Герой — нагорода квесту за ратушу 3: без цього кроку ланцюжок мовчав би, доки гравець сам не знайде «Забрати»
    {
      key: "intro.claim-hero",
      mode: "block",
      title: t("tutorial:intro.claim-hero.title"),
      text: t("tutorial:intro.claim-hero.text"),
      targets: ["claim:intro_townhall_3", "claim:intro_townhall_2", "nav:/quests"],
      available: (s) =>
        quest(s, "intro_townhall_2")?.state === QUEST_STATE.completed || quest(s, "intro_townhall_3")?.state === QUEST_STATE.completed,
      done: (s) => quest(s, "intro_townhall_3")?.state === QUEST_STATE.claimed,
    },
    {
      key: "intro.hero",
      mode: "guide",
      title: t("tutorial:intro.hero.title"),
      text: t("tutorial:intro.hero.text"),
      targets: ["hero-card", "nav:/heroes"],
      available: (s) => (s.heroes?.heroes.length ?? 0) > 0,
    },
    {
      key: "intro.march",
      mode: "guide",
      title: t("tutorial:intro.march.title"),
      text: t("tutorial:intro.march.text"),
      targets: ["send-march", "attack", "nav:/map"],
      available: (s) =>
        (s.heroes?.heroes.some((h) => h.state === "Idle") ?? false) && armySize(s) > 0,
      done: (s) => (s.marches?.length ?? 0) > 0 || (s.reports?.length ?? 0) > 0,
    },
    {
      key: "intro.report",
      mode: "guide",
      title: t("tutorial:intro.report.title"),
      text: t("tutorial:intro.report.text"),
      targets: ["reports", "nav:/map"],
      available: (s) => (s.reports?.length ?? 0) > 0,
    },
    {
      key: "intro.next",
      mode: "hint",
      title: t("tutorial:intro.next.title"),
      text: t("tutorial:intro.next.text"),
      targets: [],
      available: (s) => s.seen.has("intro.report"),
    },
  ];
}

/**
 * Що нового приносить рівень ратуші — рядок на будівлю (рішення 22.10.2026: одна картка на рівень
 * замість картки на кожну будівлю). Будівля, якої тут немає, отримує загальний рядок.
 */
const TOWN_HALL_LINES = [
  "heroeshall",
  "ironmine",
  "range",
  "farm",
  "hospital",
  "market",
  "spearyard",
  "messhall",
  "wall",
  "scouttower",
  "embassy",
  "forge",
  "fishinghut",
  "beastpen",
] as const;

type TownHallLine = (typeof TOWN_HALL_LINES)[number];

function isTownHallLine(type: string): type is TownHallLine {
  return (TOWN_HALL_LINES as readonly string[]).includes(type);
}

/** Розділи, що відкриваються ратушею, але не є будівлями села. */
function townHallFeatures(level: number): string[] {
  switch (level) {
    case 3:
      return [t("tutorial:townhall.features.tower")];
    case 5:
      return [t("tutorial:townhall.features.dungeons")];
    default:
      return [];
  }
}

/**
 * Контекстні підказки: спливають один раз, коли механіка стала доступною чи вперше трапилась.
 * Порядок у масиві — пріоритет: атака на село важливіша за будь-що інше.
 */
export function hintSteps(s: TutorialSnapshot): TutorialStep[] {
  const steps: TutorialStep[] = [];
  const level = townHallLevel(s);

  steps.push({
    key: "hint.incoming-attack",
    mode: "guide",
    title: t("tutorial:hint.incoming-attack.title"),
    text: t("tutorial:hint.incoming-attack.text"),
    targets: ["nav:/map"],
    available: (st) => st.incoming?.some((attack) => attack.mine) ?? false,
  });

  // Перший візит на екран: підказка про те, що саме тут відкрито, — поки гравець дивиться на нього.
  // Змішані тригери (екран або предмет у рюкзаку) — в кінці, щоб не перебивали підказку поточного екрана
  steps.push(
    {
      key: "hint.tower",
      mode: "hint",
      title: t("tutorial:hint.tower.title"),
      text: t("tutorial:hint.tower.text"),
      targets: [],
      available: (st) => st.route === "/tower",
    },
    {
      key: "hint.banners",
      mode: "hint",
      title: t("tutorial:hint.banners.title"),
      text: t("tutorial:hint.banners.text"),
      targets: [],
      available: (st) => st.route === "/banners",
    },
    {
      key: "hint.market",
      mode: "hint",
      title: t("tutorial:hint.market.title"),
      text: t("tutorial:hint.market.text"),
      targets: [],
      available: (st) => st.route === "/market",
    },
    {
      key: "hint.dungeons",
      mode: "hint",
      title: t("tutorial:hint.dungeons.title"),
      text: t("tutorial:hint.dungeons.text"),
      targets: [],
      available: (st) => st.route === "/dungeons",
    },
    {
      key: "hint.beastpen",
      mode: "hint",
      title: t("tutorial:hint.beastpen.title"),
      text: t("tutorial:hint.beastpen.text"),
      targets: [],
      available: (st) => st.route === "/beasts",
    },
    {
      key: "hint.clan",
      mode: "hint",
      title: t("tutorial:hint.clan.title"),
      text: t("tutorial:hint.clan.text"),
      targets: [],
      available: (st) => st.route === "/clan" && st.clan === null,
    },
    {
      key: "hint.rating",
      mode: "hint",
      title: t("tutorial:hint.rating.title"),
      text: t("tutorial:hint.rating.text"),
      targets: [],
      available: (st) => st.route === "/rating",
    },
    {
      key: "hint.codex",
      mode: "hint",
      title: t("tutorial:hint.codex.title"),
      text: t("tutorial:hint.codex.text"),
      targets: [],
      available: (st) => st.route === "/heroes/codex",
    },
    {
      key: "hint.scout",
      mode: "hint",
      title: t("tutorial:hint.scout.title"),
      text: t("tutorial:hint.scout.text"),
      targets: [],
      available: (st) => st.route === "/map" && unlocked(st, "scouttower"),
    },
    {
      key: "hint.key-chests",
      mode: "hint",
      title: t("tutorial:hint.key-chests.title"),
      text: t("tutorial:hint.key-chests.text"),
      targets: ["nav:/shop"],
      available: (st) => st.route === "/shop" || hasItem(st, (item) => item.itemKey === "key_rare" || item.itemKey === "key_unique"),
    },
    {
      key: "hint.sets",
      mode: "hint",
      title: t("tutorial:hint.sets.title"),
      text: t("tutorial:hint.sets.text"),
      targets: ["nav:/inventory"],
      available: (st) => st.route === "/inventory/sets" || hasSetPair(st),
    },
  );

  // Картка лише для поточного рівня: гравець, що давно пройшов ці рівні, не отримає стос карток
  const opened = s.village.buildings.flatMap((b) => {
    const config = s.catalog.building(b.type);
    return config !== null && config.requiresMainBuildingLevel === level ? [{ type: b.type, name: config.displayName }] : [];
  });
  const lines = [
    ...opened.map(({ type, name }) =>
      isTownHallLine(type) ? t(`tutorial:townhall.lines.${type}`) : t("tutorial:townhall.other", { name }),
    ),
    ...townHallFeatures(level),
  ];

  if (level >= 2 && lines.length > 0) {
    steps.push({
      key: `hint.townhall.${level}`,
      mode: "hint",
      title: t("tutorial:townhall.title", { level }),
      text: lines.join(" "),
      targets: [...opened.map(({ type }) => `building:${type}`), "nav:/"],
      available: () => true,
    });
  }

  steps.push(
    {
      key: "hint.shield-end",
      mode: "hint",
      title: t("tutorial:hint.shield-end.title"),
      text: t("tutorial:hint.shield-end.text"),
      targets: ["building:warehouse", "building:bank", "nav:/"],
      available: (st) => townHallLevel(st) >= NEWBIE_SHIELD_LEVEL,
    },
    {
      key: "hint.city-damage",
      mode: "hint",
      title: t("tutorial:hint.city-damage.title"),
      text: t("tutorial:hint.city-damage.text"),
      targets: [],
      available: (st) => (st.village.damage?.defeatStreak ?? 0) > 0,
    },
    {
      key: "hint.hunger",
      mode: "hint",
      title: t("tutorial:hint.hunger.title"),
      text: t("tutorial:hint.hunger.text"),
      targets: ["building:farm", "building:messhall", "nav:/"],
      available: (st) => st.village.isHungry,
    },
    {
      key: "hint.hero-xp",
      mode: "hint",
      title: t("tutorial:hint.hero-xp.title"),
      text: t("tutorial:hint.hero-xp.text"),
      targets: ["nav:/heroes"],
      available: (st) => (st.heroes?.experience ?? 0) > 0 && (st.heroes?.heroes.length ?? 0) > 0,
    },
    {
      key: "hint.first-artifact",
      mode: "hint",
      title: t("tutorial:hint.first-artifact.title"),
      text: t("tutorial:hint.first-artifact.text"),
      targets: ["equip-best", "nav:/heroes"],
      available: (st) => st.inventory?.equipment.some((piece) => piece.equippedByHeroId == null) ?? false,
    },
    {
      key: "hint.mail",
      mode: "hint",
      title: t("tutorial:hint.mail.title"),
      text: t("tutorial:hint.mail.text"),
      targets: ["nav:/mail"],
      available: (st) => st.unreadMail > 0,
    },
    {
      key: "hint.chests",
      mode: "hint",
      title: t("tutorial:hint.chests.title"),
      text: t("tutorial:hint.chests.text"),
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.type === "lootbox"),
    },
  );

  // Перший тиждень: механіка вперше трапилась у стані гри
  steps.push(
    {
      key: "hint.march-roles",
      mode: "hint",
      title: t("tutorial:hint.march-roles.title"),
      text: t("tutorial:hint.march-roles.text"),
      targets: ["nav:/map"],
      available: (st) => (st.heroes?.heroes.length ?? 0) >= 2,
    },
    {
      key: "hint.type-advantage",
      mode: "hint",
      title: t("tutorial:hint.type-advantage.title"),
      text: t("tutorial:hint.type-advantage.text"),
      targets: [],
      available: (st) => new Set((st.garrison?.units ?? []).filter((u) => u.count > 0).map((u) => u.unitType)).size >= 2,
    },
    {
      key: "hint.camp",
      mode: "hint",
      title: t("tutorial:hint.camp.title"),
      text: t("tutorial:hint.camp.text"),
      targets: ["nav:/heroes"],
      available: (st) => {
        const camp = st.heroes?.camp;
        const free = camp?.slots.some((slot) => slot.heroId == null && slot.cooldownUntil == null) ?? false;

        return camp !== undefined && camp.available && free && (st.heroes?.heroes.length ?? 0) > camp.referenceSize;
      },
    },
    {
      key: "hint.stars",
      mode: "hint",
      title: t("tutorial:hint.stars.title"),
      text: t("tutorial:hint.stars.text"),
      targets: ["nav:/heroes"],
      available: (st) => st.heroes?.heroes.some((h) => h.nextStarPartCost != null && h.shards >= h.nextStarPartCost) ?? false,
    },
    {
      key: "hint.skill-books",
      mode: "hint",
      title: t("tutorial:hint.skill-books.title"),
      text: t("tutorial:hint.skill-books.text"),
      targets: ["nav:/heroes"],
      available: (st) => hasItem(st, (item) => item.itemKey.startsWith("skill_book_")),
    },
    {
      key: "hint.wrench",
      mode: "hint",
      title: t("tutorial:hint.wrench.title"),
      text: t("tutorial:hint.wrench.text"),
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.itemKey.startsWith("wrench_")),
    },
    {
      key: "hint.forge",
      mode: "hint",
      title: t("tutorial:hint.forge.title"),
      text: t("tutorial:hint.forge.text"),
      targets: ["building:forge", "nav:/"],
      available: (st) => unlocked(st, "forge") && hasItem(st, (item) => item.itemKey === st.catalog.masteryStoneItemKey),
    },
    {
      key: "hint.beast-tamed",
      mode: "hint",
      title: t("tutorial:hint.beast-tamed.title"),
      text: t("tutorial:hint.beast-tamed.text"),
      targets: ["nav:/beasts"],
      available: (st) => (st.beastPen?.beasts.length ?? 0) > 0,
    },
    {
      key: "hint.beasts-hungry",
      mode: "hint",
      title: t("tutorial:hint.beasts-hungry.title"),
      text: t("tutorial:hint.beasts-hungry.text"),
      targets: ["building:fishinghut", "nav:/"],
      available: (st) => (st.beastPen?.beasts.length ?? 0) > 0 && st.beastPen?.isHungry === true,
    },
    {
      key: "hint.evolution",
      mode: "hint",
      title: t("tutorial:hint.evolution.title"),
      text: t("tutorial:hint.evolution.text"),
      targets: ["nav:/heroes"],
      available: (st) => st.heroes?.heroes.some((h) => h.level >= h.maxLevel && h.tier < st.catalog.maxTier) ?? false,
    },
    {
      key: "hint.weapon",
      mode: "hint",
      title: t("tutorial:hint.weapon.title"),
      text: t("tutorial:hint.weapon.text"),
      targets: ["nav:/heroes"],
      available: (st) =>
        (st.heroes?.heroes.some((h) => h.nextWeaponCost != null && h.weaponShards >= h.nextWeaponCost) ?? false)
        || hasItem(st, (item) => item.itemKey.startsWith("weapon_chest")),
    },
  );

  // Пізніше: клан, переміщення, прискорення, клавіші
  steps.push(
    {
      key: "hint.clan-help",
      mode: "hint",
      title: t("tutorial:hint.clan-help.title"),
      text: t("tutorial:hint.clan-help.text"),
      targets: ["nav:/clan"],
      available: (st) => st.clanHelp?.some((item) => !item.isMine && !item.alreadyHelped) ?? false,
    },
    {
      key: "hint.reinforce",
      mode: "hint",
      title: t("tutorial:hint.reinforce.title"),
      text: t("tutorial:hint.reinforce.text"),
      targets: ["building:embassy", "nav:/"],
      available: (st) => st.clan != null && unlocked(st, "embassy"),
    },
    {
      key: "hint.teleport",
      mode: "hint",
      title: t("tutorial:hint.teleport.title"),
      text: t("tutorial:hint.teleport.text"),
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.type === "teleport"),
    },
    {
      key: "hint.speedup-items",
      mode: "hint",
      title: t("tutorial:hint.speedup-items.title"),
      text: t("tutorial:hint.speedup-items.text"),
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.type === "speedup"),
    },
    {
      key: "hint.hotkeys",
      mode: "hint",
      title: t("tutorial:hint.hotkeys.title"),
      text: t("tutorial:hint.hotkeys.text"),
      targets: [],
      // Лише з мишею й після ланцюжка: на телефоні клавіш немає, а посеред навчання це зайвий шум
      available: (st) => st.pointerFine && (st.seen.has("intro.next") || st.skipped) && st.route !== "/",
    },
  );

  steps.push(
    {
      key: "hint.wounded",
      mode: "hint",
      title: t("tutorial:hint.wounded.title"),
      text: t("tutorial:hint.wounded.text"),
      targets: ["building:hospital", "nav:/"],
      available: (st) => (st.garrison?.wounded.some((w) => w.count > 0) ?? false),
    },
    {
      key: "hint.recoverable",
      mode: "hint",
      title: t("tutorial:hint.recoverable.title"),
      text: t("tutorial:hint.recoverable.text"),
      targets: ["building:hospital", "nav:/"],
      available: (st) => (st.garrison?.recoverable.some((r) => r.count > 0) ?? false),
    },
    {
      key: "hint.unit-level",
      mode: "hint",
      title: t("tutorial:hint.unit-level.title"),
      text: t("tutorial:hint.unit-level.text", {
        bonus: Math.round(s.catalog.unitLevelStatBonus * 100),
        max: s.catalog.maxUnitLevel,
      }),
      targets: ["building:barracks", "nav:/"],
      // На ратуші 1 стеля рівня юніта — 1: прокачувати нема куди
      available: (st) => armySize(st) >= 5 && st.seen.has("intro.train") && townHallLevel(st) >= 2,
    },
    {
      key: "hint.shards",
      mode: "hint",
      title: t("tutorial:hint.shards.title"),
      text: t("tutorial:hint.shards.text"),
      targets: ["nav:/heroes"],
      available: (st) => (st.heroes?.shards.length ?? 0) > 0 || hasItem(st, (item) => item.itemKey.startsWith("universal_shard")),
    },
    {
      key: "hint.gems",
      mode: "hint",
      title: t("tutorial:hint.gems.title"),
      text: t("tutorial:hint.gems.text"),
      targets: [],
      available: (st) => (st.wallet?.gemBalance ?? 0) > 0 && st.seen.has("intro.timers"),
    },
    {
      key: "hint.daily",
      mode: "hint",
      title: t("tutorial:hint.daily.title"),
      text: t("tutorial:hint.daily.text"),
      targets: ["nav:/quests"],
      available: (st) => (st.quests?.some((q) => q.window === QUEST_WINDOW.daily) ?? false) && st.seen.has("intro.claim"),
    },
  );

  return steps;
}

/** Крок веденого ланцюжка: після нього підказки не ставлять паузу. */
export function isIntroStep(step: TutorialStep): boolean {
  return step.key.startsWith("intro.");
}

/**
 * Активний крок. Ведений ланцюжок іде першим і по порядку; після пропуску
 * лишаються лише контекстні підказки. Побачений крок без done не повторюється,
 * а з done — закривається станом гри незалежно від seen.
 */
export function activeStep(s: TutorialSnapshot): TutorialStep | null {
  if (!s.skipped) {
    for (const step of introSteps()) {
      if (!step.available(s)) continue;

      const closed = step.done === undefined ? s.seen.has(step.key) : step.done(s) || s.seen.has(step.key);

      if (!closed) return step;
    }
  }

  // Підказки не перебивають ведений ланцюжок: він завжди доходить сюди лише коли вичерпаний
  if (s.hintsPaused) return null;

  return hintSteps(s).find((step) => !s.seen.has(step.key) && step.available(s)) ?? null;
}
