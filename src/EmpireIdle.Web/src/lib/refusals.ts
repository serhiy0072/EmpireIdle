/**
 * Тексти відмов для гравця за ключем причини з сервера.
 *
 * Ключі — з refusals/reasons.json у корені репозиторію: його генерує
 * контрактний тест на беку з RefusalReasons. Тип нижче виводиться з того
 * файлу, тож новий ключ без тексту тут не пройде typecheck.
 */
type RefusalKey = keyof typeof import("../../../../refusals/reasons.json");

type RefusalArgs = Record<string, string | number>;

const TEXTS: { [K in RefusalKey]: (args: RefusalArgs) => string } = {};

/** Текст відмови або null, якщо причини немає чи клієнт її ще не знає. */
export function refusalText(reason: string | undefined, args: RefusalArgs | undefined): string | null {
  if (reason === undefined) return null;

  const text = (TEXTS as Record<string, ((args: RefusalArgs) => string) | undefined>)[reason];

  return text === undefined ? null : text(args ?? {});
}
