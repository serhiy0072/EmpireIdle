import { t } from "i18next";
export interface BuildingScreen {
  to: string;
  label: string;
}

type ScreenKey = "quests" | "camp" | "beastPen" | "infirmary" | "heroes" | "gear" | "inventory" | "market" | "clan" | "worldMap" | "dungeons" | "research";

/**
 * Екрани, які «живуть» у будівлі: з картки будівлі на мапі села є переходи.
 * Ключі — як у buildings.json; будівля без запису переходів не має.
 * Список, а не один запис: вежа розвідки веде і на мапу, і до данжів.
 */
const SCREENS: Record<string, readonly { to: string; label: ScreenKey }[]> = {
  townhall: [{ to: "/quests", label: "quests" }],
  barracks: [{ to: "/army/barracks", label: "camp" }],
  spearyard: [{ to: "/army/spearyard", label: "camp" }],
  range: [{ to: "/army/range", label: "camp" }],
  beastpen: [{ to: "/beasts", label: "beastPen" }],
  hospital: [{ to: "/army", label: "infirmary" }],
  heroeshall: [{ to: "/heroes", label: "heroes" }],
  forge: [{ to: "/inventory?tab=equipment", label: "gear" }],
  warehouse: [{ to: "/inventory", label: "inventory" }],
  // Одна будівля на всю торгівлю: крамниця, банери й ринок гравців — вкладки (MarketTabs)
  market: [{ to: "/shop", label: "market" }],
  embassy: [{ to: "/clan", label: "clan" }],
  laboratory: [{ to: "/laboratory", label: "research" }],
  scouttower: [
    { to: "/map", label: "worldMap" },
    { to: "/dungeons", label: "dungeons" },
  ],
};

export function screensFor(buildingKey: string): readonly BuildingScreen[] {
  return (SCREENS[buildingKey] ?? []).map(({ to, label }) => ({ to, label: t(`village:screen.${label}`) }));
}
