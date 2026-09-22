/**
 * Маніфест спрайтів будівель. Ключ — тип будівлі, значення — файли за тіром
 * (індекс 0 — базовий). Порожній маніфест = усі будівлі процедурні.
 *
 * Явний список, а не <image onError>: без 404 на кожну будівлю при
 * кожному відкритті села й без миготіння між спрайтом і силуетом.
 */
export interface Sprite {
  /** Шлях від кореня сайту. */
  href: string;
  /** Співвідношення висота/ширина PNG — щоб задати висоту без завантаження. */
  aspect: number;
}

const SPRITES: Record<string, Sprite[]> = {
  // Приклад після додавання арту:
  // townhall: [
  //   { href: "/sprites/townhall.png", aspect: 1 },
  //   { href: "/sprites/townhall_2.png", aspect: 1.1 },
  // ],
};

/** Тір оздоблення: 0 — рівні 1–4, 1 — 5–9, 2 — 10+. Спільний для спрайтів і силуетів. */
export type Tier = 0 | 1 | 2;

/** Тір за рівнем — той самий поділ, що й у README спрайтів: 1–4, 5–9, 10+. */
export function tierOf(level: number): Tier {
  return level >= 10 ? 2 : level >= 5 ? 1 : 0;
}

/** Спрайт для будівлі або null — тоді малюється силует. Тір без файлу відкочується до нижчого. */
export function spriteFor(buildingType: string, level: number): Sprite | null {
  const variants = SPRITES[buildingType];

  if (variants === undefined || variants.length === 0) return null;

  return variants[Math.min(tierOf(level), variants.length - 1)] ?? null;
}
