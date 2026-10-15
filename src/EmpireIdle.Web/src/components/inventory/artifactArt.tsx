import type { ReactElement } from "react";

/**
 * Іконки артефактів за набором. Кожен набір — одна родина: свій метал, камінь і емблема
 * на всіх чотирьох предметах, а форма — від слота. Так кольє й пояс одного набору
 * впізнаються як пара, а два кольє різних наборів не плутаються.
 *
 * Справжній арт підключається маніфестом ARTIFACT_ART: ключ — «набір_слот»
 * (dawn_necklace), рідкість показує рамка, тож окремий файл на рідкість не потрібен.
 * Явний список, а не <img onError>: без 404 і миготіння. Конвенція файлів — public/sprites/README.md.
 */
const ARTIFACT_ART: Record<string, string> = {
  // Приклад після додавання арту:
  // dawn_necklace: "/sprites/artifacts/dawn_necklace.png",
};

interface SetStyle {
  /** Метал оправи. */
  metal: string;
  /** Тінь і контур металу. */
  edge: string;
  /** Камінь або емаль. */
  gem: string;
  /** Колір емблеми — контрастний до каменя. */
  mark: string;
  /** Емблема набору на камені. */
  emblem: (color: string) => ReactElement;
}

const sun = (c: string) => (
  <g>
    <circle cx={0} cy={0} r={4} fill={c} />
    <path d="M0 -8 L0 -6 M0 6 L0 8 M-8 0 L-6 0 M6 0 L8 0 M-5.5 -5.5 L-4 -4 M4 4 L5.5 5.5 M-5.5 5.5 L-4 4 M4 -4 L5.5 -5.5" stroke={c} strokeWidth={1.6} strokeLinecap="round" />
  </g>
);
const flame = (c: string) => <path d="M0 -8 Q6 -2 4 3 Q3 7 0 8 Q-3 7 -4 3 Q-5 -1 0 -8 Z M0 -1 Q2 2 1 5 Q0 6 -1 5 Q-2 2 0 -1 Z" fill={c} fillRule="evenodd" />;
const snowflake = (c: string) => (
  <path d="M0 -8 L0 8 M-7 -4 L7 4 M-7 4 L7 -4 M-2 -6 L0 -4 L2 -6 M-2 6 L0 4 L2 6" stroke={c} strokeWidth={1.6} strokeLinecap="round" fill="none" />
);
const wave = (c: string) => <path d="M-8 2 Q-4 -4 0 2 Q4 8 8 2 M-8 -3 Q-4 -9 0 -3 Q4 3 8 -3" stroke={c} strokeWidth={1.6} fill="none" strokeLinecap="round" />;
const bolt = (c: string) => <path d="M2 -8 L-4 1 L0 1 L-2 8 L4 -1 L0 -1 Z" fill={c} />;
const thorn = (c: string) => <path d="M-7 7 Q0 0 7 -7 M-3 3 L-6 -1 M1 -1 L4 3 M3 -3 L0 -7" stroke={c} strokeWidth={1.6} fill="none" strokeLinecap="round" />;
const ember = (c: string) => (
  <g fill={c}>
    <circle cx={-3} cy={2} r={2} />
    <circle cx={3} cy={-1} r={1.6} />
    <circle cx={0} cy={-5} r={1.2} />
    <circle cx={2} cy={5} r={1} />
  </g>
);
const voidRing = (c: string) => (
  <g fill="none" stroke={c} strokeWidth={1.6}>
    <circle cx={0} cy={0} r={6} />
    <circle cx={0} cy={0} r={2} fill={c} />
  </g>
);
const swirl = (c: string) => <path d="M-7 -2 Q-2 -8 4 -4 Q8 0 3 3 Q0 4 -1 1 M-7 4 Q0 9 6 5" stroke={c} strokeWidth={1.6} fill="none" strokeLinecap="round" />;
const shard = (c: string) => <path d="M0 -8 L5 0 L0 8 L-5 0 Z M0 -8 L0 8" stroke={c} strokeWidth={1.2} fill="none" />;

/** Палітра й мотиви наборів — від Режисера (15.10.2026); контур — темніший тон металу. */
const SET_STYLES: Record<string, SetStyle> = {
  dawn: { metal: "#E8C66A", edge: "#8A6D1F", gem: "#3E7C7A", mark: "#F3E9D2", emblem: sun },
  ember: { metal: "#4A3528", edge: "#2B1D16", gem: "#D9541E", mark: "#F2A33A", emblem: flame },
  frost: { metal: "#CFDDE6", edge: "#4A78A8", gem: "#BFE6F5", mark: "#4A78A8", emblem: snowflake },
  tide: { metal: "#E3D5B8", edge: "#1F6F8B", gem: "#7FD1C7", mark: "#1F6F8B", emblem: wave },
  storm: { metal: "#9AA3C7", edge: "#3B3F6B", gem: "#3B3F6B", mark: "#F5E663", emblem: bolt },
  thorn: { metal: "#6B4E2E", edge: "#3F2C18", gem: "#3F5A2A", mark: "#D8C3A5", emblem: thorn },
  ash: { metal: "#C9B79C", edge: "#5A5552", gem: "#5A5552", mark: "#E0602A", emblem: ember },
  hollow: { metal: "#B8B5C9", edge: "#1C1A2E", gem: "#1C1A2E", mark: "#6C5CE7", emblem: voidRing },
  gale: { metal: "#C9A227", edge: "#7A6216", gem: "#8FB3D9", mark: "#F0F4F7", emblem: swirl },
  obsidian: { metal: "#4B4F58", edge: "#0E0E10", gem: "#0E0E10", mark: "#B9382B", emblem: shard },
};

/** Камінь з емблемою набору в точці (x, y). */
function gem(style: SetStyle, x: number, y: number, scale = 1): ReactElement {
  return (
    <g transform={`translate(${x} ${y}) scale(${scale})`}>
      <circle r={11} fill={style.gem} stroke={style.edge} strokeWidth={2} />
      {style.emblem(style.mark)}
    </g>
  );
}

const SLOTS: Record<string, (style: SetStyle) => ReactElement> = {
  necklace: (s) => (
    <g>
      <path d="M28 20 Q50 58 72 20" fill="none" stroke={s.metal} strokeWidth={4} />
      <path d="M28 20 Q50 58 72 20" fill="none" stroke={s.edge} strokeWidth={1} />
      <path d="M50 44 L66 60 L50 82 L34 60 Z" fill={s.metal} stroke={s.edge} strokeWidth={2} strokeLinejoin="round" />
      {gem(s, 50, 61, 0.9)}
    </g>
  ),
  ring: (s) => (
    <g>
      <circle cx={50} cy={60} r={20} fill="none" stroke={s.edge} strokeWidth={10} />
      <circle cx={50} cy={60} r={20} fill="none" stroke={s.metal} strokeWidth={7} />
      {gem(s, 50, 36, 1.05)}
    </g>
  ),
  crown: (s) => (
    <g>
      <path d="M22 70 L26 32 L40 50 L50 26 L60 50 L74 32 L78 70 Z" fill={s.metal} stroke={s.edge} strokeWidth={2.5} strokeLinejoin="round" />
      <rect x={22} y={66} width={56} height={10} rx={2} fill={s.edge} />
      {gem(s, 50, 56, 0.85)}
    </g>
  ),
  belt: (s) => (
    <g>
      <rect x={12} y={42} width={76} height={18} rx={4} fill={s.edge} />
      <rect x={12} y={45} width={76} height={12} rx={3} fill={s.metal} opacity={0.55} />
      <rect x={34} y={34} width={32} height={34} rx={6} fill={s.metal} stroke={s.edge} strokeWidth={2.5} />
      {gem(s, 50, 51, 0.9)}
    </g>
  ),
};

const ARTIFACT_KEY = /^([a-z]+)_(necklace|ring|crown|belt)_/;

/** Набір і слот артефакта з його ключа: dawn_necklace_rare → dawn, necklace. */
function parse(itemKey: string): { set: string; slot: string } | null {
  const match = ARTIFACT_KEY.exec(itemKey);
  return match?.[1] != null && match[2] != null ? { set: match[1], slot: match[2] } : null;
}

/** Файл справжнього арту для артефакта або null — тоді малюється процедурна іконка набору. */
export function artifactArtFor(itemKey: string): string | null {
  const parsed = parse(itemKey);
  return parsed === null ? null : (ARTIFACT_ART[`${parsed.set}_${parsed.slot}`] ?? null);
}

/** Процедурна іконка артефакта в стилі його набору або null — ключ не з відомого набору. */
export function drawArtifact(itemKey: string): ReactElement | null {
  const parsed = parse(itemKey);
  const style = parsed === null ? undefined : SET_STYLES[parsed.set];
  const slot = parsed === null ? undefined : SLOTS[parsed.slot];

  return style === undefined || slot === undefined ? null : slot(style);
}
