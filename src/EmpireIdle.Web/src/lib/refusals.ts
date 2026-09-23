/**
 * Тексти відмов для гравця за ключем причини з сервера.
 *
 * Ключі — з refusals/reasons.json у корені репозиторію: його генерує
 * контрактний тест на беку з RefusalReasons. Тип нижче виводиться з того
 * файлу, тож новий ключ без тексту тут не пройде typecheck.
 */
type RefusalKey = keyof typeof import("../../../../refusals/reasons.json");

type RefusalArgs = Record<string, string | number>;

const TEXTS: { [K in RefusalKey]: (args: RefusalArgs) => string } = {
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
