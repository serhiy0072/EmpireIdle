/** Назви класів героїв. Ключі — як у конфігу сервера. */
export const CLASS_LABELS: Record<string, string> = {
  warrior: "Воїн",
  knight: "Лицар",
  archer: "Лучник",
};

/** Невідомий клас повертається ключем: краще сирий рядок, ніж порожнє місце. */
export function classLabel(heroClass: string | null | undefined): string {
  return heroClass == null ? "—" : (CLASS_LABELS[heroClass] ?? heroClass);
}
