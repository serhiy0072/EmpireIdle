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
