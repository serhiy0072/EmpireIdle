/**
 * Кольори місцевості за ключем з конфіга map.json. Невідомий тип
 * малюється нейтральним: новий терен на сервері не ламає мапу.
 */
const TERRAIN: Record<string, { fill: string; label: string }> = {
  plain: { fill: "#d9f99d", label: "Рівнина" },
  forest: { fill: "#4ade80", label: "Ліс" },
  mountain: { fill: "#a8a29e", label: "Гори" },
  water: { fill: "#7dd3fc", label: "Вода" },
  peaks: { fill: "#e7e5e4", label: "Скелі" },
  swamp: { fill: "#84cc16", label: "Болото" },
};

export function terrainFill(type: string): string {
  return TERRAIN[type]?.fill ?? "#e2e8f0";
}

export function terrainLabel(type: string): string {
  return TERRAIN[type]?.label ?? type;
}
