import type {
  BattleReportResponse,
  GarrisonResponse,
  MarchResponse,
  QuestView,
  VillageResponse,
  WalletResponse,
} from "../lib/apiTypes";
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
    text: "Кожен апгрейд займає час. Поки триває — збирайте ресурси, тренуйте військо або прискорте його за самоцвіти 💎: вони приходять за квести.",
    targets: [],
    available: (s) => building(s, MAIN_BUILDING)?.isUnderConstruction === true,
  },
  {
    key: "intro.collect",
    mode: "guide",
    title: "Зберіть золото",
    text: "Шахта копає золото сама, навіть коли вас немає. Коли над нею з'явиться бульбашка, клацніть на шахту й натисніть «Зібрати» — 50 золота закриють перший квест.",
    targets: ["collect", `building:${GOLD_MINE}`, "nav:/"],
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
    text: "Казарми вже стоять. Відкрийте «Військо» й натренуйте 5 піхотинців — без армії немає походів.",
    targets: [`train:${FIRST_UNIT}`, "nav:/army"],
    available: () => true,
    done: (s) => armySize(s) >= 5 || (s.garrison?.trainingOrders.length ?? 0) > 0,
  },
  {
    key: "intro.townhall3",
    mode: "guide",
    title: "Ратуша 3 рівня",
    text: "Третій рівень ратуші відкриє ферму й приведе першого героя. Збирайте ресурси й покращуйте.",
    targets: [`building:${MAIN_BUILDING}`, "nav:/"],
    available: (s) => reachedOrBuilding(s, MAIN_BUILDING, 2),
    done: (s) => reachedOrBuilding(s, MAIN_BUILDING, 3),
  },
  {
    key: "intro.hero",
    mode: "guide",
    title: "Ваш перший герой",
    text: "Герой веде армію в похід: без нього марш не вирушить. Подивіться його вміння у вкладці «Герої».",
    targets: ["hero-card", "nav:/heroes"],
    available: (s) => (s.heroes?.heroes.length ?? 0) > 0,
  },
  {
    key: "intro.march",
    mode: "guide",
    title: "Перший похід",
    text: "Знайдіть на мапі монстра ☠ поруч із селом, натисніть «Атакувати», оберіть військо й героя. «Оцінити шанси» покаже, чи варто.",
    targets: ["send-march", "attack", "nav:/map"],
    available: (s) =>
      (s.heroes?.heroes.some((h) => h.state === "Idle") ?? false) && armySize(s) > 0,
    done: (s) => (s.marches?.length ?? 0) > 0 || (s.reports?.length ?? 0) > 0,
  },
  {
    key: "intro.report",
    mode: "guide",
    title: "Звіт бою",
    text: "Бій відбувся. Звіт показує, хто вцілів, хто поранений (їх лікує госпіталь) і кого можна викупити.",
    targets: ["reports", "nav:/map"],
    available: (s) => (s.reports?.length ?? 0) > 0,
  },
];

/** Гейт, з якого нова будівля варта окремої підказки: перші (казарми, банк) уже пояснює ланцюжок. */
const UNLOCK_HINT_MIN_GATE = 2;

/**
 * Контекстні підказки: спливають один раз, коли в грі з'являється нове.
 * Обчислюються з каталогу й стану, тож нова будівля в конфізі отримує
 * підказку без правок тут.
 */
export function hintSteps(s: TutorialSnapshot): TutorialStep[] {
  const steps: TutorialStep[] = [];

  for (const b of s.village.buildings) {
    const config = s.catalog.building(b.type);

    if (config === null || config.requiresMainBuildingLevel < UNLOCK_HINT_MIN_GATE || !b.isUnlocked) continue;

    steps.push({
      key: `hint.unlock.${b.type}`,
      mode: "hint",
      title: `Відкрито: ${config.displayName}`,
      text:
        config.producesResource != null
          ? `Нова будівля добуває ${s.catalog.resourceName(config.producesResource)}. Знайдіть її на мапі села й покращуйте — виробіток росте з рівнем.`
          : `Нова будівля з'явилась у селі. Клацніть на неї, щоб побачити, що вона дає.`,
      targets: [`building:${b.type}`, "nav:/"],
      available: () => true,
    });
  }

  steps.push(
    {
      key: "hint.wounded",
      mode: "hint",
      title: "Поранені",
      text: "Частина війська після бою не гине, а лягає в госпіталь. Вилікуйте їх ресурсами або самоцвітами у вкладці «Військо».",
      targets: ["nav:/army"],
      available: (st) => (st.garrison?.wounded.some((w) => w.count > 0) ?? false),
    },
    {
      key: "hint.recoverable",
      mode: "hint",
      title: "Юніти до викупу",
      text: "Загиблих можна повернути за самоцвіти, але лише до дедлайну — далі вони згорають. Дивіться «Військо» → «Госпіталь».",
      targets: ["nav:/army"],
      available: (st) => (st.garrison?.recoverable.some((r) => r.count > 0) ?? false),
    },
    {
      key: "hint.unit-level",
      mode: "hint",
      title: "Прокачка юнітів",
      text: "Навчених воїнів можна підняти на вищий рівень: +10% до сили за рівень. Це дешевше, ніж тренувати нових на високому рівні.",
      targets: ["nav:/army"],
      available: (st) => armySize(st) >= 5 && st.seen.has("intro.train"),
    },
    {
      key: "hint.shards",
      mode: "hint",
      title: "Уламки героїв",
      text: "Уламки звичайних героїв купуються за золото в залі героїв — з боїв вони не падають. Дублікат героя з банера підвищує сузір'я, а понад стелю сузір'я — дає печатки призову.",
      targets: ["nav:/heroes"],
      available: (st) => (st.heroes?.shards.length ?? 0) > 0,
    },
    {
      key: "hint.gems",
      mode: "hint",
      title: "Самоцвіти",
      text: "💎 прискорюють будівництво, тренування й походи, лікують поранених і викупають загиблих. Витрачайте на те, що заважає найбільше.",
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
  return hintSteps(s).find((step) => !s.seen.has(step.key) && step.available(s)) ?? null;
}
