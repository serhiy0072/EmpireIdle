export interface BuildingScreen {
  to: string;
  label: string;
}

/**
 * Екрани, які «живуть» у будівлі: з картки будівлі на мапі села є переходи.
 * Ключі — як у buildings.json; будівля без запису переходів не має.
 * Список, а не один запис: вежа розвідки веде і на мапу, і до данжів.
 */
const SCREENS: Record<string, readonly BuildingScreen[]> = {
  townhall: [{ to: "/quests", label: "Квести" }],
  barracks: [{ to: "/army/barracks", label: "Табір" }],
  spearyard: [{ to: "/army/spearyard", label: "Табір" }],
  range: [{ to: "/army/range", label: "Табір" }],
  beastpen: [{ to: "/beasts", label: "Звіринець" }],
  hospital: [{ to: "/army", label: "Лазарет" }],
  heroeshall: [{ to: "/heroes", label: "Герої" }],
  lootshop: [{ to: "/banners", label: "Банери" }],
  forge: [{ to: "/forge", label: "Кузня" }],
  warehouse: [{ to: "/inventory", label: "Інвентар" }],
  market: [
    { to: "/market", label: "Ринок гравців" },
    { to: "/shop", label: "Крамниця" },
  ],
  embassy: [{ to: "/clan", label: "Клан" }],
  scouttower: [
    { to: "/map", label: "Мапа світу" },
    { to: "/dungeons", label: "Данжі" },
  ],
};

export function screensFor(buildingKey: string): readonly BuildingScreen[] {
  return SCREENS[buildingKey] ?? [];
}
