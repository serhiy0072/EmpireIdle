import { t } from "i18next";
/**
 * Кольори місцевості за ключем з конфіга map.json. Невідомий тип
 * малюється нейтральним: новий терен на сервері не ламає мапу.
 */
const TERRAIN: Record<string, { fill: string; label: TerrainKey }> = {
  plain: { fill: "#d9f99d", label: "plain" },
  forest: { fill: "#4ade80", label: "forest" },
  mountain: { fill: "#a8a29e", label: "mountain" },
  water: { fill: "#7dd3fc", label: "water" },
  peaks: { fill: "#e7e5e4", label: "peaks" },
  swamp: { fill: "#84cc16", label: "swamp" },
};

type TerrainKey = "plain" | "forest" | "mountain" | "water" | "peaks" | "swamp";

export function terrainFill(type: string): string {
  return TERRAIN[type]?.fill ?? "#e2e8f0";
}

export function terrainLabel(type: string): string {
  const label = TERRAIN[type]?.label;

  return label === undefined ? type : t(`common:terrain.${label}`);
}
