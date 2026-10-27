/**
 * Кумулятивна вартість чи час тренування/прокачки юніта — дзеркалить
 * ProgressionCurves.CumulativeUnitLevelCost на бекенді (§5.2 GDD): сума
 * кроків від fromLevel до toLevel-1 за геометричною кривою growth^(level-1).
 * Спільний для тренування з нуля (fromLevel=1) і прокачки наявного стека.
 */
export function cumulativeUnitLevelCost(base: number, fromLevel: number, toLevel: number, growth: number): number {
  let total = 0;

  for (let level = fromLevel; level < toLevel; level++) {
    total += base * growth ** (level - 1);
  }

  return Math.trunc(total);
}

/**
 * Множник атаки й захисту юніта від рівня — дзеркалить ProgressionCurves.UnitStatMultiplier на бекенді:
 * +bonusPerLevel за рівень (catalog.unitLevelStatBonus), рівень 1 — база. Швидкість і вантаж від рівня не ростуть.
 */
export function unitStatMultiplier(level: number, bonusPerLevel: number): number {
  return 1 + bonusPerLevel * (level - 1);
}
