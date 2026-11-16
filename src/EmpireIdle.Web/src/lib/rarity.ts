import { rankLabel, rankStyle } from "./queries/catalog";

/** Рідкість із сервера — "Common", "Rare" або "Unique"; регістр нормалізуємо, бо ключі конфігу бувають малими. */
export function rarityKey(value: string | undefined): string {
  if (value === undefined || value === "") return "Common";

  return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase();
}

export function rarityStyle(value: string | undefined): string {
  return rankStyle(rarityKey(value));
}

export function rarityLabel(value: string | undefined): string {
  return rankLabel(rarityKey(value));
}
