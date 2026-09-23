/**
 * Тексти відмов для гравця за ключем причини з сервера.
 *
 * Ключі — з refusals/reasons.json у корені репозиторію: його генерує
 * контрактний тест на беку з RefusalReasons. Тип нижче виводиться з того
 * файлу, тож новий ключ без тексту тут не пройде typecheck.
 */
import { resourceGenitive } from "./resourceNames";

type RefusalKey = keyof typeof import("../../../../refusals/reasons.json");

type RefusalArgs = Record<string, string | number>;

const TEXTS: { [K in RefusalKey]: (args: RefusalArgs) => string } = {
  // ---------- Спільні ----------
  "common.buildingRequired": ({ building }) => `Потрібна будівля «${building}»`,

  // ---------- Село й будівлі ----------
  "village.storageFull": ({ resource }) =>
    `Склад ${resourceGenitive(String(resource))} заповнений — витратьте частину, перш ніж збирати`,
  "village.alreadyThere": () => "Поселення вже стоїть на цій клітинці",
  "building.serverCeiling": ({ serverLevel, ceiling }) =>
    `На рівні світу ${serverLevel} будівлі ростуть лише до ${ceiling} рівня`,
  "building.townHallCeiling": ({ building, level }) =>
    `«${building}» не може бути вищою за ратушу (${level} рівень) — спершу підніміть ратушу`,
  "building.villageLagging": ({ level, buildings }) =>
    `Перш ніж ратуша перейде на новий тір, підтягніть до ${level} рівня: ${buildings}`,
  "building.underConstruction": () => "Ця будівля вже будується",
  "building.alreadyCompleted": () => "Будівництво вже завершено",

  // ---------- Герої ----------
  "hero.onTheMove": () => "Герой зараз у поході — дочекайтеся його повернення",
  "hero.levelCeiling": ({ hero, ceiling }) =>
    `${hero} досяг стелі ${ceiling} рівня: підніміть ратушу або еволюціонуйте тір`,
  "hero.trainingBusy": () => "Зала героїв уже тренує іншого героя",
  "hero.worldLevelRequired": ({ required, current }) =>
    `Наступний тір відкриється на рівні світу ${required} (зараз ${current})`,
  "hero.evolutionItemRequired": ({ item }) => `Для еволюції потрібен предмет «${item}»`,
  "hero.maxTier": ({ tier }) => `Герой уже на найвищому тірі (${tier})`,
  "hero.notEnoughShards": ({ hero, need, have }) => `Для призову «${hero}» потрібно ${need} уламків, зібрано ${have}`,

  // ---------- Спорядження ----------
  "equipment.maxEnhancement": ({ max }) => `Уже максимальне посилення +${max}`,
  "equipment.broken": () => "Спершу відремонтуйте предмет у кузні",
  "equipment.alreadyEquipped": ({ item }) => `«${item}» уже вдягнено на цього героя`,
  "equipment.classMismatch": ({ weapon }) => `«${weapon}» не підходить класу цього героя`,

  // ---------- Данжі ----------
  "dungeon.townHallRequired": ({ dungeon, level }) => `«${dungeon}» відкривається з ратушею ${level} рівня`,
  "dungeon.levelLocked": ({ level, previous }) => `Рівень ${level} відкриється після проходження рівня ${previous}`,
  "dungeon.heroWounded": ({ hero }) => `${hero} зараз у госпіталі — оберіть іншого героя`,
  "dungeon.runInProgress": () => "У вас уже є незавершений забіг — поверніться до нього",
  "dungeon.targetUnreachable": () => "Цю ціль зараз не дістати: спершу бийте передню лінію або того, хто провокує",
};

/** Текст відмови або null, якщо причини немає чи клієнт її ще не знає. */
export function refusalText(reason: string | undefined, args: RefusalArgs | undefined): string | null {
  if (reason === undefined) return null;

  const text = (TEXTS as Record<string, ((args: RefusalArgs) => string) | undefined>)[reason];

  return text === undefined ? null : text(args ?? {});
}
