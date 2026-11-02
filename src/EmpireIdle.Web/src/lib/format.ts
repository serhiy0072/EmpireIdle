import { currentLanguage, type Language } from "./i18n";

/**
 * Числа й дати мовою інтерфейсу. Мова читається в момент виклику: при її зміні
 * екрани перемонтовуються (App), тож підписка тут не потрібна.
 */
const LOCALES: Record<Language, string> = { uk: "uk-UA", en: "en-US" };

function locale(): string {
  return LOCALES[currentLanguage()];
}

export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return value.toLocaleString(locale(), options);
}

/** Час у грі завжди 24-годинний (рішення 28.10.2026): таймери й розклад мають читатися однаково. */
export function formatDateTime(value: string | number | Date, options: Intl.DateTimeFormatOptions): string {
  return new Date(value).toLocaleString(locale(), { hourCycle: "h23", ...options });
}

/** «14:05» — година й хвилини. */
export function formatClock(value: string | number | Date): string {
  return formatDateTime(value, { hour: "2-digit", minute: "2-digit" });
}

/** «28 жовт., 14:05» — момент у межах найближчих днів: листи, щит, кінець акції. */
export function formatMoment(value: string | number | Date): string {
  return formatDateTime(value, { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}
