/**
 * Екран, який «живе» в будівлі: з картки будівлі на мапі села є перехід.
 * Ключі — як у buildings.json; будівля без запису переходу не має.
 */
const SCREENS: Record<string, { to: string; label: string }> = {
  townhall: { to: "/quests", label: "Квести" },
  barracks: { to: "/army", label: "Військо" },
  stable: { to: "/army", label: "Військо" },
  beastpen: { to: "/army", label: "Військо" },
  hospital: { to: "/army", label: "Лазарет" },
  heroeshall: { to: "/heroes", label: "Герої" },
  lootshop: { to: "/banners", label: "Банери" },
  forge: { to: "/forge", label: "Кузня" },
  warehouse: { to: "/inventory", label: "Інвентар" },
  market: { to: "/shop", label: "Крамниця" },
  embassy: { to: "/clan", label: "Клан" },
  scouttower: { to: "/map", label: "Мапа світу" },
};

export function screenFor(buildingKey: string): { to: string; label: string } | null {
  return SCREENS[buildingKey] ?? null;
}
