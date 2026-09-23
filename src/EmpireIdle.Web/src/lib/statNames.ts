/** Назви статів спорядження й наборів. Ключі — як у конфігу сервера. */
const STAT_LABELS: Record<string, string> = {
  Attack: "Атака",
  Defense: "Захист",
  Health: "Здоров'я",
};

/** Невідомий стат повертається ключем: краще сирий рядок, ніж порожнє місце. */
export function statLabel(key: string): string {
  return STAT_LABELS[key] ?? key;
}
