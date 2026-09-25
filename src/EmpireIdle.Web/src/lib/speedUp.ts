/**
 * Підпис кнопки прискорення. null — ціна невідома, показуємо просто дію.
 * Безкоштовного прискорення немає: на межі (ціна 0) кнопку не показують зовсім.
 */
export function speedUpLabel(costGems: number | null): string {
  if (costGems === null) return "Прискорити";

  return `Прискорити (${costGems.toLocaleString("uk-UA")} 💎)`;
}
