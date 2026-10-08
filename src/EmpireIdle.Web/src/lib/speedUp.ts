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
