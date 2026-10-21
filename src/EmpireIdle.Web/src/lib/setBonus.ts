import type { CatalogArtifactSet } from "./queries/catalog";

export interface SetThresholdView {
  /** Скільки предметів родини треба вдягнути. */
  required: number;
  /** Рідкість, з якої взято числа: досягнутого порогу — N-го предмета, недосягнутого — переданої. */
  rarity: string;
  bonus: Record<string, number>;
  active: boolean;
}

/**
 * Пороги набору для героя (рішення 15.10.2026) — так само, як рахує сервер у HeroStats.SetBonus: предмети
 * родини рахуються будь-якої рідкості, поріг N діє з N предметів, а числа бере з рідкості N-го предмета
 * від найрідкіснішого. Недосягнутий поріг показує числа fallbackRarity — рідкості предмета на картці:
 * що дасть набір, якщо добрати решту такої самої.
 */
export function setThresholds(set: CatalogArtifactSet, wornKeys: string[], fallbackRarity: string): SetThresholdView[] {
  // Рідкості в каталозі йдуть від звичайної до унікальної — індекс і є ранг
  const rank = (rarity: string) => set.rarities.findIndex((group) => group.rarity === rarity);

  const worn = wornKeys
    .map((key) => set.rarities.find((group) => group.pieceKeys.includes(key))?.rarity)
    .filter((rarity): rarity is string => rarity !== undefined)
    .sort((a, b) => rank(b) - rank(a));

  const required = [...new Set(set.rarities.flatMap((group) => group.thresholds.map((t) => t.requiredPieces)))].sort((a, b) => a - b);

  return required.map((count) => {
    const active = worn.length >= count;
    const rarity = active ? (worn[count - 1] ?? fallbackRarity) : fallbackRarity;
    const bonus = set.rarities.find((group) => group.rarity === rarity)?.thresholds.find((t) => t.requiredPieces === count)?.bonus ?? {};

    return { required: count, rarity, bonus, active };
  });
}

/** Скільки предметів родини вдягнено на героя. */
export function wornOfSet(set: CatalogArtifactSet, wornKeys: string[]): number {
  return wornKeys.filter((key) => set.rarities.some((group) => group.pieceKeys.includes(key))).length;
}
