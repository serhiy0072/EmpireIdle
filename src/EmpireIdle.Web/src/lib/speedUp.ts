/**
 * Підпис кнопки прискорення. null — ціна невідома, показуємо просто дію.
 * Безкоштовного прискорення немає: на межі (ціна 0) кнопку не показують зовсім.
 */
export function speedUpLabel(costGems: number | null): string {
  if (costGems === null) return "Прискорити";

  return `Прискорити (${costGems.toLocaleString("uk-UA")} 💎)`;
}

/** Тривалість предмета-прискорення коротко, як на плитці: «5 хв», «1 год», «12 год». */
export function speedUpMinutesLabel(minutes: number): string {
  return minutes < 60 ? `${minutes} хв` : `${minutes / 60} год`;
}

/** Прискорення в рюкзаку: скільки хвилин дає одне й скільки штук є. */
export interface SpeedUpStock {
  itemKey: string;
  rarity: string;
  minutes: number;
  count: number;
}

/**
 * Найменший набір без зайвого: спершу довгі, поки влазять у залишок, а хвіст закриває
 * найкоротше, що його покриває. Жадібно, бо прискорень сім видів — перебір не потрібен.
 */
export function suggestSpeedUps(stock: SpeedUpStock[], minutesLeft: number): Record<string, number> {
  const picked: Record<string, number> = {};
  let rest = minutesLeft;

  for (const item of [...stock].sort((a, b) => b.minutes - a.minutes)) {
    const take = Math.min(item.count, Math.floor(rest / item.minutes));
    if (take > 0) {
      picked[item.itemKey] = take;
      rest -= take * item.minutes;
    }
  }

  if (rest > 0) {
    const cover = [...stock]
      .sort((a, b) => a.minutes - b.minutes)
      .find((item) => item.count - (picked[item.itemKey] ?? 0) > 0 && item.minutes >= rest)
      ?? [...stock].sort((a, b) => b.minutes - a.minutes).find((item) => item.count - (picked[item.itemKey] ?? 0) > 0);

    if (cover !== undefined) picked[cover.itemKey] = (picked[cover.itemKey] ?? 0) + 1;
  }

  return picked;
}
