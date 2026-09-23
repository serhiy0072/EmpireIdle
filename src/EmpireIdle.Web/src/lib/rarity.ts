import { rankLabel, rankStyle } from "./queries/catalog";

/**
 * Рідкість приходить по-різному: у каталозі героїв — "Rare", в інвентарі — "rare",
 * у банерах — числом з enum Rarity. Зводимо до одного ключа й стилю.
 */
const BY_NUMBER: Record<number, string> = { 1: "Common", 2: "Rare", 3: "Unique" };

export function rarityKey(value: string | number | undefined): string {
  if (typeof value === "number") return BY_NUMBER[value] ?? "Common";
  if (value === undefined || value === "") return "Common";

  return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase();
}

export function rarityStyle(value: string | number | undefined): string {
  return rankStyle(rarityKey(value));
}

export function rarityLabel(value: string | number | undefined): string {
  return rankLabel(rarityKey(value));
}
