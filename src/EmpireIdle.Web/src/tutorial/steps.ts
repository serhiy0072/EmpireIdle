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

/** Ведений ланцюжок. Порядок у масиві — порядок показу. */
export const INTRO_STEPS: TutorialStep[] = [
  {
    key: "intro.welcome",
    mode: "block",
    title: "Вітаємо в EmpireIdle",
    text: "Ви отримали село, шахту й запас золота та дерева. За кілька кроків покажемо, як тут усе працює — або пропустіть навчання й розбирайтесь самі.",
    targets: [],
    available: () => true,
  },
  // Апгрейд першим: стартових ресурсів на нього вистачає, а шахта ще порожня —
  // блокувати гравця, поки вона накопичить 50 золота, означало б п'ять хвилин нудьги
  {
    key: "intro.upgrade",
    mode: "block",
    title: "Покращте ратушу",
    text: "Ратуша відкриває інші будівлі, ресурси й героїв. Клацніть на неї й натисніть «Покращити» — стартових запасів вистачить.",
    targets: ["upgrade", `building:${MAIN_BUILDING}`, "nav:/"],
    available: () => true,
    done: (s) => reachedOrBuilding(s, MAIN_BUILDING, 2),
  },
  {
    key: "intro.timers",
    mode: "hint",
    title: "Будівництво триває",
    text: "Кожен апгрейд займає час. Поки триває — тренуйте військо або прискорте його за самоцвіти 💎: вони приходять за квести.",
    targets: [],
    available: (s) => building(s, MAIN_BUILDING)?.isUnderConstruction === true,
  },
  {
    key: "intro.collect",
    mode: "guide",
    title: "Золото йде саме",
    text: "Шахта копає золото сама, навіть коли вас немає, і щохвилини складає його на склад. Зачекайте трохи — 50 зібраного золота закриють перший квест.",
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
    title: "Заберіть нагороду",
    text: "Перший квест виконано. Нагороди самі не приходять — відкрийте «Квести» й натисніть «Забрати».",
    targets: ["claim:intro_farm", "nav:/quests"],
    available: (s) => quest(s, "intro_farm")?.state === QUEST_STATE.completed,
    done: (s) => quest(s, "intro_farm")?.state === QUEST_STATE.claimed,
  },
  {
    key: "intro.train",
    mode: "guide",
    title: "Навчіть перших воїнів",
    text: "Казарма вже стоїть. Торкніться її в селі, відкрийте «Табір» і натренуйте 5 піхотинців — по 15 золота за кожного. Без війська походів немає.",
    targets: [`train:${FIRST_UNIT}`, "camp:barracks", "building:barracks", "nav:/"],
    available: () => true,
    done: (s) => armySize(s) >= 5 || (s.garrison?.trainingOrders.length ?? 0) > 0,
  },
  {
    key: "intro.townhall3",
    mode: "guide",
    title: "Ратуша 3 рівня",
    text: "3-й рівень ратуші відкриє ферму, госпіталь, ринок і Вежу, а квест «Серце поселення» приведе першого героя. Збирайте ресурси й покращуйте.",
    targets: [`building:${MAIN_BUILDING}`, "nav:/"],
    available: (s) => reachedOrBuilding(s, MAIN_BUILDING, 2),
    done: (s) => reachedOrBuilding(s, MAIN_BUILDING, 3),
  },
  // Герой — нагорода квесту за ратушу 3: без цього кроку ланцюжок мовчав би, доки гравець сам не знайде «Забрати»
  {
    key: "intro.claim-hero",
    mode: "block",
    title: "Заберіть нагороди",
    text: "Квести за ратушу виконано. Заберіть нагороди — за 3-й рівень на вас чекає перший герой.",
    targets: ["claim:intro_townhall_3", "claim:intro_townhall_2", "nav:/quests"],
    available: (s) =>
      quest(s, "intro_townhall_2")?.state === QUEST_STATE.completed || quest(s, "intro_townhall_3")?.state === QUEST_STATE.completed,
    done: (s) => quest(s, "intro_townhall_3")?.state === QUEST_STATE.claimed,
  },
  {
    key: "intro.hero",
    mode: "guide",
    title: "Ваш перший герой",
    text: "Герой веде армію: без нього марш не вирушить. Кожен герой веде війська своєї ролі: воїн — піхоту, лицар — списоносців, лучник — лучників. Подивіться його вміння у вкладці «Герої».",
    targets: ["hero-card", "nav:/heroes"],
    available: (s) => (s.heroes?.heroes.length ?? 0) > 0,
  },
  {
    key: "intro.march",
    mode: "guide",
    title: "Перший похід",
    text: "Відкрийте мапу, оберіть монстра поруч із селом і натисніть «Атакувати». Форма сама візьме найсильніших вільних героїв (по одному на роль) і максимум війська та покаже прогноз бою — лишається натиснути «Відправити».",
    targets: ["send-march", "attack", "nav:/map"],
    available: (s) =>
      (s.heroes?.heroes.some((h) => h.state === "Idle") ?? false) && armySize(s) > 0,
    done: (s) => (s.marches?.length ?? 0) > 0 || (s.reports?.length ?? 0) > 0,
  },
  {
    key: "intro.report",
    mode: "guide",
    title: "Звіт бою",
    text: "Бій відбувся. У звіті видно, хто вцілів і хто поранений: поранені лежать у госпіталі, їх лікують ресурсами або 💎. Частину загиблих можна викупити за 10 💎 кожного, але лише 24 години.",
    targets: ["reports", "nav:/map"],
    available: (s) => (s.reports?.length ?? 0) > 0,
  },
  {
    key: "intro.next",
    mode: "hint",
    title: "Далі — самі",
    text: "Квести поведуть далі: ратуша 5, перші данжі, звіринець. Нові можливості ми пояснимо, щойно вони відкриються.",
    targets: [],
    available: (s) => s.seen.has("intro.report"),
  },
];

/**
 * Що нового приносить рівень ратуші — рядок на будівлю (рішення 22.10.2026: одна картка на рівень
 * замість картки на кожну будівлю). Будівля, якої тут немає, отримує загальний рядок.
 */
const TOWN_HALL_LINES: Record<string, string> = {
  heroeshall: "Зала героїв — тут живуть і ростуть ваші герої.",
  ironmine: "Залізна копальня — залізо для списоносців, муру й лікування героїв.",
  range: "Стрільбище — лучники, сильні проти списоносців.",
  farm: "Ферма — м'ясо, яким живиться село.",
  hospital: "Госпіталь — сюди лягають поранені після боїв.",
  market: "Ринок — крамниця, банери героїв, торгівля з гравцями й безкоштовні скрині щодня.",
  spearyard: "Двір списоносців — списоносці, сильні проти піхоти.",
  messhall: "Їдальня — щогодини з'їдає м'ясо, і що більше село, то більше.",
  wall: "Мур — +3% до оборони села за рівень.",
  scouttower: "Вежа розвідки — розвідка чужих сіл і вхід у данжі.",
  embassy: "Посольство — приймає підкріплення союзників, 50 місць за рівень.",
  forge: "Кузня — заточка артефактів за точильні камені.",
  fishinghut: "Рибальська хижа — корм для звірів.",
  beastpen: "Звіринець — приручені звірі дають бонуси.",
};

/** Розділи, що відкриваються ратушею, але не є будівлями села. */
const TOWN_HALL_FEATURES: Record<number, string[]> = {
  3: ["Тисячна вежа — кнопка «Вежа» внизу: поверх за поверхом, нагорода за кожен."],
  5: ["Данжі — забіги загону героїв за артефактами наборів; вхід — через Вежу розвідки."],
};

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
    title: "На вас іде атака",
    text: "Ворожий марш уже в дорозі — час прибуття видно на мапі. Витратьте зайве понад захищене, дотренуйте військо або прискорте мур.",
    targets: ["nav:/map"],
    available: (st) => st.incoming?.some((attack) => attack.mine) ?? false,
  });

  // Перший візит на екран: підказка про те, що саме тут відкрито, — поки гравець дивиться на нього.
  // Змішані тригери (екран або предмет у рюкзаку) — в кінці, щоб не перебивали підказку поточного екрана
  steps.push(
    {
      key: "hint.tower",
      mode: "hint",
      title: "Тисячна вежа",
      text: "Кожен поверх — бій ваших героїв проти загону вежі. За поверх — 20 💎 і досвід героїв, на кожному 5-му — рідкісний ключ, на кожному 10-му — ще й артефакт і унікальний ключ.",
      targets: [],
      available: (st) => st.route === "/tower",
    },
    {
      key: "hint.banners",
      mode: "hint",
      title: "Банери героїв",
      text: "Призов коштує 1000 💎 (стандартний — 1200). Рідкісна нагорода гарантована не пізніше 10-ї спроби, унікальна — не пізніше 50-ї (у стандартного — 60-ї). Усі шанси — під «Шанси».",
      targets: [],
      available: (st) => st.route === "/banners",
    },
    {
      key: "hint.market",
      mode: "hint",
      title: "Ринок гравців",
      text: "Продавайте й купуйте в інших гравців. Лот висить 48 годин, податок — 5%, одночасно до 10 лотів; куплене можна перепродати лише через 72 години.",
      targets: [],
      available: (st) => st.route === "/market",
    },
    {
      key: "hint.dungeons",
      mode: "hint",
      title: "Данжі",
      text: "Загін із 4 героїв проходить хвилі ворогів; воїни й лицарі стоять попереду. Забіг коштує 10 енергії зі 100, повна шкала відновлюється за 20 годин. Кожен данж дає артефакти свого набору й досвід героям.",
      targets: [],
      available: (st) => st.route === "/dungeons",
    },
    {
      key: "hint.beastpen",
      mode: "hint",
      title: "Звіринець",
      text: "Звіра приручають на мапі: «Приручити» на монстрі його виду — це звичайний бій, і перемога з шансом приводить звіра. Не пізніше 10-ї перемоги звір гарантований. «Знайти» відкриє найближчого.",
      targets: [],
      available: (st) => st.route === "/beasts",
    },
    {
      key: "hint.clan",
      mode: "hint",
      title: "Клан",
      text: "Співклановці скорочують ваші таймери: кожен помічник — 2%, разом до 40%. На території клану ваші війська б'ються на 10% сильніше.",
      targets: [],
      available: (st) => st.route === "/clan" && st.clan === null,
    },
    {
      key: "hint.rating",
      mode: "hint",
      title: "Рейтинг",
      text: "Рейтинг рахує силу (55%), розвиток (25%) і активність (20%).",
      targets: [],
      available: (st) => st.route === "/rating",
    },
    {
      key: "hint.codex",
      mode: "hint",
      title: "Кодекс героїв",
      text: "Кодекс — усі герої гри й де їх шукати.",
      targets: [],
      available: (st) => st.route === "/heroes/codex",
    },
    {
      key: "hint.scout",
      mode: "hint",
      title: "Розвідка",
      text: "Перед атакою на село розвідайте його: звіт прийде в скриньку. Завіса туману на 24 години ховає ваше село від розвідки.",
      targets: [],
      available: (st) => st.route === "/map" && unlocked(st, "scouttower"),
    },
    {
      key: "hint.key-chests",
      mode: "hint",
      title: "Скрині за ключі",
      text: "Рідкісну скриню можна відкрити безкоштовно 5 разів на день, з перервою 5 хв, унікальну — раз на 48 годин. Понад це потрібні ключі: їх дає Вежа й продає крамниця.",
      targets: ["nav:/shop"],
      available: (st) => st.route === "/shop" || hasItem(st, (item) => item.itemKey === "key_rare" || item.itemKey === "key_unique"),
    },
    {
      key: "hint.sets",
      mode: "hint",
      title: "Набори артефактів",
      text: "2 і 4 предмети одного набору на герої його класу дають додаткові бонуси. Набори падають у данжах — кожен данж має свій.",
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
    ...opened.map(({ type, name }) => TOWN_HALL_LINES[type] ?? `${name} — клацніть на неї в селі, щоб побачити, що вона дає.`),
    ...(TOWN_HALL_FEATURES[level] ?? []),
  ];

  if (level >= 2 && lines.length > 0) {
    steps.push({
      key: `hint.townhall.${level}`,
      mode: "hint",
      title: `Ратуша ${level}: нове в селі`,
      text: lines.join(" "),
      targets: [...opened.map(({ type }) => `building:${type}`), "nav:/"],
      available: () => true,
    });
  }

  steps.push(
    {
      key: "hint.shield-end",
      mode: "hint",
      title: "Захист новачка знято",
      text: "Поки ратуша була нижче 3 рівня, на вас не могли напасти. Тепер можуть: від грабунку захищено 250 ресурсу за кожен рівень складу, а золота — банку. Решту вкладайте в будівництво, а не тримайте.",
      targets: ["building:warehouse", "building:bank", "nav:/"],
      available: (st) => townHallLevel(st) >= NEWBIE_SHIELD_LEVEL,
    },
    {
      key: "hint.city-damage",
      mode: "hint",
      title: "Село пограбоване",
      text: "Поразка пошкодила будівлі: вони видобувають удвічі менше, доки не відновляться за 7 годин або не будуть відремонтовані. Кілька поразок поспіль без відновлення виселять село з місця.",
      targets: [],
      available: (st) => (st.village.damage?.defeatStreak ?? 0) > 0,
    },
    {
      key: "hint.hunger",
      mode: "hint",
      title: "Село голодує",
      text: "Їдальні бракує м'яса: видобуток усього, крім м'яса, впав на 25%, а оборона — на 20%. Покращуйте ферму або поповніть м'ясо з ящика.",
      targets: ["building:farm", "building:messhall", "nav:/"],
      available: (st) => st.village.isHungry,
    },
    {
      key: "hint.hero-xp",
      mode: "hint",
      title: "Досвід героїв",
      text: "Досвід за монстрів, Вежу й данжі йде в спільний запас. Витрачайте його на рівень будь-якого героя на його екрані.",
      targets: ["nav:/heroes"],
      available: (st) => (st.heroes?.experience ?? 0) > 0 && (st.heroes?.heroes.length ?? 0) > 0,
    },
    {
      key: "hint.first-artifact",
      mode: "hint",
      title: "Перший артефакт",
      text: "Артефакти вдягають герої: намисто, корона, кільце й пояс. Відкрийте героя → спорядження: кнопка «Швидке використання» вдягне найкраще вільне.",
      targets: ["equip-best", "nav:/heroes"],
      available: (st) => st.inventory?.equipment.some((piece) => piece.equippedByHeroId == null) ?? false,
    },
    {
      key: "hint.mail",
      mode: "hint",
      title: "Скринька",
      text: "Нагороди за вхід, квестові нагороди й звіти розвідки приходять листами. Нагороди за вхід згорають, щойно почнеться наступний день чи тиждень, — заходьте й забирайте.",
      targets: ["nav:/mail"],
      available: (st) => st.unreadMail > 0,
    },
    {
      key: "hint.chests",
      mode: "hint",
      title: "Скрині",
      text: "Скрині відкриваються в рюкзаку, можна цілим стосом. Гарантія з опису («не пізніше N-ї скрині») працює чесно: лічильник зберігається між відкриттями.",
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.type === "lootbox"),
    },
  );

  // Перший тиждень: механіка вперше трапилась у стані гри
  steps.push(
    {
      key: "hint.march-roles",
      mode: "hint",
      title: "Марш і колони",
      text: "У марш іде від 1 до 3 героїв, і кожен веде колони війська своєї ролі. Колона — 100 воїнів; на 1 рівні героя їх 2, на 10-му — 3, і далі більше. «Порівну» ділить військо між маршами, які ще можна відправити.",
      targets: ["nav:/map"],
      available: (st) => (st.heroes?.heroes.length ?? 0) >= 2,
    },
    {
      key: "hint.type-advantage",
      mode: "hint",
      title: "Хто кого б'є",
      text: "Піхота б'є лучників, лучники — списоносців, списоносці — піхоту: +35% урону по слабшому. Місцевість теж важить: у лісі сильніша піхота, у горах — лучники.",
      targets: [],
      available: (st) => new Set((st.garrison?.units ?? []).filter((u) => u.count > 0).map((u) => u.unitType)).size >= 2,
    },
    {
      key: "hint.camp",
      mode: "hint",
      title: "Навчальний табір",
      text: "Герой у таборі одразу отримує рівень найслабшого з ваших 5 найсильніших героїв — без витрати досвіду. Вийнятий герой звільняє слот на 24 години.",
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
      title: "Зірки героя",
      text: "Уламки купують частини зірок: 6 частин — зірка, максимум 5 зірок. Кожна частина посилює героя, а кожна зірка відкриває ще один рівень умінь.",
      targets: ["nav:/heroes"],
      available: (st) => st.heroes?.heroes.some((h) => h.nextStarPartCost != null && h.shards >= h.nextStarPartCost) ?? false,
    },
    {
      key: "hint.skill-books",
      mode: "hint",
      title: "Книги вмінь",
      text: "Книга піднімає вміння героя свого класу й рідкості: рівні коштують 3, 5, 7, 10 і 12 книг. Кожен наступний рівень потребує ще однієї зірки героя.",
      targets: ["nav:/heroes"],
      available: (st) => hasItem(st, (item) => item.itemKey.startsWith("skill_book_")),
    },
    {
      key: "hint.wrench",
      mode: "hint",
      title: "Гайкові ключі",
      text: "Ключ дає артефакту досвід рівня — 100 або 300, як згодований звичайний чи рідкісний артефакт.",
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.itemKey.startsWith("wrench_")),
    },
    {
      key: "hint.forge",
      mode: "hint",
      title: "Заточка",
      text: "Кузня заточує артефакти за точильні камені аж до +20. До +5 заточка завжди вдала, далі шанс падає до 40%, а з +11 невдача може зламати артефакт — його лагодить ремкомплект або 💎.",
      targets: ["building:forge", "nav:/"],
      available: (st) => unlocked(st, "forge") && hasItem(st, (item) => item.itemKey === st.catalog.masteryStoneItemKey),
    },
    {
      key: "hint.beast-tamed",
      mode: "hint",
      title: "Ваш звір",
      text: "Ласощі піднімають звірові рівень і посилюють його бонус. Бонус вмикається за корм і діє кілька годин, а утримання звіринця щогодини з'їдає корм.",
      targets: ["nav:/beasts"],
      available: (st) => (st.beastPen?.beasts.length ?? 0) > 0,
    },
    {
      key: "hint.beasts-hungry",
      mode: "hint",
      title: "Звірам бракує корму",
      text: "Без корму бонуси звірів не вмикаються. Покращуйте рибальську хижу.",
      targets: ["building:fishinghut", "nav:/"],
      available: (st) => (st.beastPen?.beasts.length ?? 0) > 0 && st.beastPen?.isHungry === true,
    },
    {
      key: "hint.evolution",
      mode: "hint",
      title: "Еволюція",
      text: "Герой досяг стелі свого тіру. Есенція Другого Кола переведе його в наступний тір (потрібен рівень світу 2), Третього Кола — ще вище (рівень світу 3).",
      targets: ["nav:/heroes"],
      available: (st) => st.heroes?.heroes.some((h) => h.level >= h.maxLevel && h.tier < st.catalog.maxTier) ?? false,
    },
    {
      key: "hint.weapon",
      mode: "hint",
      title: "Зброя героя",
      text: "Шматки зброї посилюють героя відсотком до характеристик, до 5 рівнів. Скриня зброї дає шматок унікальної зброї героя банера на ваш вибір.",
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
      title: "Допомога клану",
      text: "Натискайте «Допомогти» — це безкоштовно й скорочує таймери союзників.",
      targets: ["nav:/clan"],
      available: (st) => st.clanHelp?.some((item) => !item.isMine && !item.alreadyHelped) ?? false,
    },
    {
      key: "hint.reinforce",
      mode: "hint",
      title: "Підкріплення",
      text: "Посольство приймає війська союзників на оборону села. Свої війська союзнику — через марш із наміром «Підкріпити».",
      targets: ["building:embassy", "nav:/"],
      available: (st) => st.clan != null && unlocked(st, "embassy"),
    },
    {
      key: "hint.teleport",
      mode: "hint",
      title: "Телепорт",
      text: "Телепорт переносить село на вільну клітину. Ближній — до 100 клітин, клановий — на територію клану.",
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.type === "teleport"),
    },
    {
      key: "hint.speedup-items",
      mode: "hint",
      title: "Прискорення",
      text: "Прискорення зрізають час із будь-якого таймера: будівництва, тренування, рівнів війська чи маршу.",
      targets: ["nav:/inventory"],
      available: (st) => hasItem(st, (item) => item.type === "speedup"),
    },
    {
      key: "hint.hotkeys",
      mode: "hint",
      title: "Клавіші",
      text: "Esc — назад або закрити вікно, Enter — підтвердити, ←/→ — гортати вкладки й сусідів.",
      targets: [],
      // Лише з мишею й після ланцюжка: на телефоні клавіш немає, а посеред навчання це зайвий шум
      available: (st) => st.pointerFine && (st.seen.has("intro.next") || st.skipped) && st.route !== "/",
    },
  );

  steps.push(
    {
      key: "hint.wounded",
      mode: "hint",
      title: "Поранені",
      text: "Частина війська після бою не гине, а лягає в госпіталь. Торкніться госпіталю в селі й вилікуйте їх ресурсами або самоцвітами.",
      targets: ["building:hospital", "nav:/"],
      available: (st) => (st.garrison?.wounded.some((w) => w.count > 0) ?? false),
    },
    {
      key: "hint.recoverable",
      mode: "hint",
      title: "Юніти до викупу",
      text: "Загиблих можна повернути за самоцвіти, але лише до дедлайну — далі вони згорають. Дивіться госпіталь у селі.",
      targets: ["building:hospital", "nav:/"],
      available: (st) => (st.garrison?.recoverable.some((r) => r.count > 0) ?? false),
    },
    {
      key: "hint.unit-level",
      mode: "hint",
      title: "Прокачка юнітів",
      text: `Навчених воїнів можна підняти на вищий рівень у таборі їхньої будівлі: +${Math.round(s.catalog.unitLevelStatBonus * 100)}% до атаки, захисту й здоров'я за рівень. Стеля — рівень ратуші, але не вище ${s.catalog.maxUnitLevel}.`,
      targets: ["building:barracks", "nav:/"],
      // На ратуші 1 стеля рівня юніта — 1: прокачувати нема куди
      available: (st) => armySize(st) >= 5 && st.seen.has("intro.train") && townHallLevel(st) >= 2,
    },
    {
      key: "hint.shards",
      mode: "hint",
      title: "Уламки героїв",
      text: "Уламки героїв падають із банерів і скринь. 10 уламків нового героя призивають його, а уламки вже наявного йдуть у його зірки. Універсальний уламок стає уламком будь-якого відкритого героя тієї ж рідкості.",
      targets: ["nav:/heroes"],
      available: (st) => (st.heroes?.shards.length ?? 0) > 0 || hasItem(st, (item) => item.itemKey.startsWith("universal_shard")),
    },
    {
      key: "hint.gems",
      mode: "hint",
      title: "Самоцвіти",
      text: "💎 прискорюють будівництво, тренування й походи, лікують поранених і викупають загиблих. Витрачайте на те, що заважає найбільше. Предмети-прискорення з рюкзака теж зрізають таймери — спершу витрачайте їх.",
      targets: [],
      available: (st) => (st.wallet?.gemBalance ?? 0) > 0 && st.seen.has("intro.timers"),
    },
    {
      key: "hint.daily",
      mode: "hint",
      title: "Щоденні квести",
      text: "Щодня о 00:00 UTC з'являються нові завдання з нагородами. Заходьте раз на день — це найшвидше джерело самоцвітів.",
      targets: ["nav:/quests"],
      available: (st) => (st.quests?.some((q) => q.window === QUEST_WINDOW.daily) ?? false) && st.seen.has("intro.claim"),
    },
  );

  return steps;
}

/**
 * Активний крок. Ведений ланцюжок іде першим і по порядку; після пропуску
 * лишаються лише контекстні підказки. Побачений крок без done не повторюється,
 * а з done — закривається станом гри незалежно від seen.
 */
export function activeStep(s: TutorialSnapshot): TutorialStep | null {
  if (!s.skipped) {
    for (const step of INTRO_STEPS) {
      if (!step.available(s)) continue;

      const closed = step.done === undefined ? s.seen.has(step.key) : step.done(s) || s.seen.has(step.key);

      if (!closed) return step;
    }
  }

  // Підказки не перебивають ведений ланцюжок: він завжди доходить сюди лише коли вичерпаний
  if (s.hintsPaused) return null;

  return hintSteps(s).find((step) => !s.seen.has(step.key) && step.available(s)) ?? null;
}
