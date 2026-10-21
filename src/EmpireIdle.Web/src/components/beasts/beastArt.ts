/**
 * Процедурний звір збоку рядком SVG — для загону (canvas) і екрана звіра (img), як heroArt для героїв.
 * Звір дивиться праворуч, лапи стоять на землі (y = 84). Кадр кроку розводить лапи навхрест:
 * два кадри по черзі — і звір іде, кадр 0 — стоїть.
 */

/** Кадр кроку: −1 і 1 — лапи навхрест у різні боки, 0 — стоїть. */
export type BeastFrame = -1 | 0 | 1;

interface Fur {
  body: string;
  dark: string;
  light: string;
  eye: string;
}

const OUTLINE = "#1f2937";

const WOLF: Fur = { body: "#9ca3af", dark: "#4b5563", light: "#e5e7eb", eye: "#fbbf24" };
const WARG: Fur = { body: "#52525b", dark: "#27272a", light: "#a1a1aa", eye: "#ef4444" };
const BOAR: Fur = { body: "#7c4a2b", dark: "#4a2c17", light: "#a8754f", eye: "#111827" };
const BEAR: Fur = { body: "#8b5a2b", dark: "#5b3a1a", light: "#c08a5a", eye: "#111827" };
const GRIFFIN: Fur = { body: "#d6a35c", dark: "#8a5a2b", light: "#f3d29b", eye: "#111827" };

/** Лапа від тулуба до землі; swing зсуває ступню вперед чи назад. */
function leg(x: number, top: number, width: number, color: string, swing: number, paw = color): string {
  const foot = x + swing;

  return (
    `<path d="M${x} ${top} L${foot} 82" stroke="${color}" stroke-width="${width}" stroke-linecap="round"/>` +
    `<ellipse cx="${foot + 1}" cy="83" rx="${width / 2 + 1}" ry="2" fill="${paw}"/>`
  );
}

/** Чотири лапи: дальні темніші й позаду, ближні — колір тулуба; пари ходять навхрест. */
function legs(back: number, front: number, top: number, width: number, fur: Fur, step: number, frontPaw?: string): string {
  return (
    leg(back - 5, top, width - 1, fur.dark, step) +
    leg(front - 5, top, width - 1, fur.dark, -step, frontPaw) +
    leg(back, top, width, fur.body, -step) +
    leg(front, top, width, fur.body, step, frontPaw)
  );
}

/** Вовк і варг — та сама стать: варг більший, темніший, з гривою шипів і червоними очима. */
function canine(fur: Fur, step: number, mane: boolean): string {
  return (
    `<path d="M32 42 Q14 40 8 26" stroke="${fur.dark}" stroke-width="8" stroke-linecap="round" fill="none"/>` +
    `<path d="M14 34 Q10 30 8 26" stroke="${fur.light}" stroke-width="5" stroke-linecap="round" fill="none"/>` +
    legs(42, 80, 50, 7, fur, step) +
    `<ellipse cx="60" cy="46" rx="30" ry="15" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.5"/>` +
    `<ellipse cx="60" cy="53" rx="21" ry="5" fill="${fur.light}" opacity="0.7"/>` +
    (mane
      ? `<path d="M46 33 l4 -9 l4 8 l4 -10 l4 9 l4 -11 l4 10 l4 -12 l5 12 Z" fill="${fur.dark}" stroke="${OUTLINE}" stroke-width="1"/>`
      : "") +
    `<path d="M84 26 L88 11 L95 24 Z" fill="${fur.dark}" stroke="${OUTLINE}" stroke-width="1"/>` +
    `<ellipse cx="92" cy="34" rx="13" ry="11" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.5"/>` +
    `<path d="M92 25 L98 11 L102 25 Z" fill="${fur.dark}" stroke="${OUTLINE}" stroke-width="1"/>` +
    `<path d="M100 30 L115 35 L113 42 L99 43 Z" fill="${fur.light}" stroke="${OUTLINE}" stroke-width="1.5" stroke-linejoin="round"/>` +
    `<circle cx="114" cy="36.5" r="2.2" fill="${OUTLINE}"/>` +
    `<circle cx="97" cy="31" r="2" fill="${fur.eye}"/>` +
    (mane ? `<path d="M102 43 l2 4 l2 -4" fill="#f8fafc"/>` : "")
  );
}

/** Вепр: важкий клин голови, щетина вздовж спини, ікла й рожевий п'ятачок. */
function boar(step: number): string {
  const fur = BOAR;

  return (
    `<path d="M28 46 q-6 -2 -4 -7 q3 -3 5 1" stroke="${fur.dark}" stroke-width="2.5" fill="none" stroke-linecap="round"/>` +
    legs(40, 78, 58, 9, fur, step * 0.7) +
    `<ellipse cx="58" cy="50" rx="32" ry="18" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.5"/>` +
    `<path d="M34 37 l3 -7 l3 6 l3 -8 l3 7 l3 -9 l3 8 l3 -9 l3 8 l3 -8 l3 7 l3 -7 l3 7 Z" fill="${fur.dark}"/>` +
    `<ellipse cx="56" cy="58" rx="22" ry="6" fill="${fur.light}" opacity="0.5"/>` +
    `<path d="M80 36 Q100 32 110 48 L113 58 Q96 64 80 60 Z" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.5" stroke-linejoin="round"/>` +
    `<path d="M83 37 L87 25 L93 37 Z" fill="${fur.dark}" stroke="${OUTLINE}" stroke-width="1"/>` +
    `<ellipse cx="111" cy="54" rx="4" ry="5.5" fill="#f9a8d4" stroke="${OUTLINE}" stroke-width="1.2"/>` +
    `<circle cx="110" cy="52" r="0.9" fill="${OUTLINE}"/><circle cx="111.5" cy="56" r="0.9" fill="${OUTLINE}"/>` +
    `<path d="M103 58 Q98 54 99 46" stroke="#fefce8" stroke-width="3.2" fill="none" stroke-linecap="round"/>` +
    `<circle cx="96" cy="44" r="2" fill="${fur.eye}"/>`
  );
}

/** Ведмідь: велика туша з горбом на плечах, круглі вуха, світла морда. */
function bear(step: number): string {
  const fur = BEAR;

  return (
    `<circle cx="20" cy="44" r="4.5" fill="${fur.dark}"/>` +
    legs(38, 78, 58, 13, fur, step * 0.8) +
    `<ellipse cx="56" cy="48" rx="36" ry="22" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.5"/>` +
    `<circle cx="76" cy="34" r="14" fill="${fur.body}"/>` +
    `<ellipse cx="54" cy="60" rx="26" ry="6" fill="${fur.light}" opacity="0.35"/>` +
    `<circle cx="88" cy="28" r="5" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.2"/>` +
    `<circle cx="96" cy="40" r="13" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.5"/>` +
    `<circle cx="99" cy="27.5" r="5" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.2"/>` +
    `<circle cx="99" cy="27.5" r="2.4" fill="${fur.light}"/>` +
    `<ellipse cx="107" cy="45" rx="8" ry="5.5" fill="${fur.light}" stroke="${OUTLINE}" stroke-width="1.2"/>` +
    `<ellipse cx="113.5" cy="43.5" rx="2.8" ry="2.2" fill="${OUTLINE}"/>` +
    `<circle cx="100" cy="36" r="2" fill="${fur.eye}"/>`
  );
}

/** Грифон: левове тіло з китицею на хвості, складені крила, орлина голова з дзьобом і пазурі спереду. */
function griffin(step: number): string {
  const fur = GRIFFIN;

  return (
    `<path d="M30 44 Q14 50 10 30" stroke="${fur.body}" stroke-width="4" fill="none" stroke-linecap="round"/>` +
    `<ellipse cx="9" cy="27" rx="4" ry="6" fill="${fur.dark}"/>` +
    legs(40, 80, 54, 8, fur, step, "#f59e0b") +
    `<ellipse cx="60" cy="48" rx="30" ry="15" fill="${fur.body}" stroke="${OUTLINE}" stroke-width="1.5"/>` +
    `<path d="M44 40 Q60 20 84 32 Q72 44 40 58 Q30 50 44 40 Z" fill="#92400e" stroke="${OUTLINE}" stroke-width="1.5" stroke-linejoin="round"/>` +
    `<path d="M46 46 Q60 34 78 36 M42 52 Q56 44 72 42" stroke="#fbbf24" stroke-width="1.6" fill="none"/>` +
    `<path d="M80 28 L74 16 M84 24 L80 12 M88 22 L88 10" stroke="#f8fafc" stroke-width="3" stroke-linecap="round"/>` +
    `<circle cx="92" cy="30" r="12" fill="#f8fafc" stroke="${OUTLINE}" stroke-width="1.5"/>` +
    `<path d="M101 25 L116 31 Q110 39 101 36 Z" fill="#f59e0b" stroke="${OUTLINE}" stroke-width="1.5" stroke-linejoin="round"/>` +
    `<circle cx="96" cy="27" r="2.2" fill="${fur.eye}"/>` +
    `<path d="M93 23 L100 24" stroke="${OUTLINE}" stroke-width="1.5"/>`
  );
}

/** Розмір звіра в загоні відносно вовка: ведмідь і грифон більші, вепр нижчий. */
export const BEAST_SCALE: Record<string, number> = { wolf: 1, warg: 1.15, boar: 1, bear: 1.25, griffin: 1.2 };

/** Тіло звіра за ключем; невідомий — вовк сірої масті, а не порожнє місце. */
function body(beastKey: string, step: number): string {
  switch (beastKey) {
    case "warg":
      return canine(WARG, step, true);
    case "boar":
      return boar(step);
    case "bear":
      return bear(step);
    case "griffin":
      return griffin(step);
    default:
      return canine(WOLF, step, false);
  }
}

export function beastSvg(beastKey: string, frame: BeastFrame = 0): string {
  return (
    // Розмір у самому SVG: без нього canvas не знає, якого розміру картинка
    `<svg xmlns="http://www.w3.org/2000/svg" width="120" height="90" viewBox="0 0 120 90">` +
    body(beastKey, frame * 5) +
    `</svg>`
  );
}

/** Той самий звір як адреса для img і для canvas. */
export function beastArtUrl(beastKey: string, frame: BeastFrame = 0): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(beastSvg(beastKey, frame))}`;
}
