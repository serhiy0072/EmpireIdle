/** Підпис кнопки прискорення. null — ціна невідома, показуємо просто дію. */
export function speedUpLabel(costGems: number | null): string {
  if (costGems === null) return "Прискорити";

  return costGems === 0 ? "Прискорити (безкоштовно)" : `Прискорити (${costGems.toLocaleString("uk-UA")} 💎)`;
}
