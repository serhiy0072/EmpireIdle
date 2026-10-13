/** Рівні юнітів у таборі: рамка кольору рівня й римська цифра, як на референсі 13.10.2026. */

/** Рамка рівня, як на референсі: низькі — сірі, середні — сині, VII–VIII — фіолетові, далі — золоті. */
export function tierFrame(level: number): { from: string; to: string } {
  if (level >= 9) return { from: "#fbbf24", to: "#c2410c" };
  if (level >= 7) return { from: "#a78bfa", to: "#6d28d9" };
  if (level >= 4) return { from: "#7dd3fc", to: "#1d4ed8" };
  return { from: "#cbd5e1", to: "#475569" };
}

const ROMAN: [number, string][] = [
  [10, "X"],
  [9, "IX"],
  [5, "V"],
  [4, "IV"],
  [1, "I"],
];

/** Рівень юніта римськими цифрами, як на плитках табору. */
export function roman(value: number): string {
  let rest = value;
  let result = "";

  for (const [step, glyph] of ROMAN) {
    while (rest >= step) {
      result += glyph;
      rest -= step;
    }
  }

  return result;
}
