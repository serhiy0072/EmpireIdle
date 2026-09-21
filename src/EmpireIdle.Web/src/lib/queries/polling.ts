/** Поки прострочене замовлення ще в відповіді — сканер не встиг: опитуємо з таким кроком. */
const OVERDUE_POLL_MS = 5_000;

/** Запас після дедлайну: сканер на сервері теж не миттєвий. */
const DUE_MARGIN_MS = 1_500;

/**
 * refetchInterval для запитів із таймерами. Завершення черг робить сканер
 * на сервері, події про це не приходять, тож клієнт сам перепитує рівно
 * тоді, коли настане найближчий дедлайн, а далі — з коротким кроком,
 * поки прострочене не зникне з відповіді.
 *
 * Функція переобчислюється після кожного fetch: новий інтервал завжди
 * рахується від свіжих даних.
 */
export function refetchAtDue<T>(dueTimes: (data: T) => Iterable<string | null | undefined>) {
  return (query: { state: { data?: T } }): number | false => {
    const data = query.state.data;

    if (data === undefined) return false;

    let next = Number.POSITIVE_INFINITY;

    for (const value of dueTimes(data)) {
      if (value === null || value === undefined) continue;

      const at = Date.parse(value);

      if (!Number.isNaN(at)) next = Math.min(next, at);
    }

    if (next === Number.POSITIVE_INFINITY) return false;

    return Math.max(next - Date.now() + DUE_MARGIN_MS, OVERDUE_POLL_MS);
  };
}
