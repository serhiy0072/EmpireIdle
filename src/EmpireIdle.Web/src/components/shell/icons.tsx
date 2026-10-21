import type { ReactElement } from "react";

/**
 * Значки ігрової оболонки — процедурні SVG у полі 0..32, як решта арту гри: нижня панель розділів
 * і ресурси в шапці. Контур темний, заливка яскрава — читаються і на синьому тлі, і на світлому.
 */

const OUTLINE = "#0f2147";

const NAV: Record<string, ReactElement> = {
  army: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M7 5 L23 21 L21 23 L5 7 Z" fill="#e2e8f0" />
      <path d="M25 5 L9 21 L11 23 L27 7 Z" fill="#e2e8f0" />
      <path d="M18 23 L23 28 M26 20 L23 23" strokeWidth={3} stroke="#92400e" />
      <path d="M14 23 L9 28 M6 20 L9 23" strokeWidth={3} stroke="#92400e" />
    </g>
  ),
  heroes: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M6 28 C6 20 10 18 16 18 C22 18 26 20 26 28 Z" fill="#3b82f6" />
      <circle cx={16} cy={11} r={6} fill="#fcd9b6" />
      <path d="M9 9 L11 3 L14 7 L16 2 L18 7 L21 3 L23 9 Z" fill="#facc15" />
    </g>
  ),
  map: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M3 7 L11 4 L21 7 L29 4 L29 25 L21 28 L11 25 L3 28 Z" fill="#fde68a" />
      <path d="M11 4 V25 M21 7 V28" />
      <path d="M6 12 C9 10 12 15 15 13 S20 17 25 14" stroke="#ef4444" strokeDasharray="2 2" fill="none" />
      <path d="M22 19 L26 23 M26 19 L22 23" stroke="#ef4444" strokeWidth={2} />
    </g>
  ),
  tower: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M10 28 L11 11 H21 L22 28 Z" fill="#cbd5e1" />
      <path d="M8 11 V6 H11 V8 H14 V6 H18 V8 H21 V6 H24 V11 Z" fill="#94a3b8" />
      <path d="M14 28 V22 A2 2 0 0 1 18 22 V28" fill="#92400e" />
      <rect x={14.5} y={14} width={3} height={4} fill="#facc15" />
      <path d="M16 6 V1 L21 3 L16 4" fill="#ef4444" />
    </g>
  ),
  quests: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M8 4 H24 V28 H8 Z" fill="#fef3c7" />
      <path d="M6 4 H26 M6 28 H26" strokeWidth={3} stroke="#92400e" />
      <path d="M11 10 H21 M11 15 H21 M11 20 H17" />
      <circle cx={23} cy={23} r={5} fill="#facc15" />
      <path d="M21 23 L22.5 24.5 L25 21.5" strokeWidth={1.5} fill="none" />
    </g>
  ),
  inventory: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M11 9 C11 4 21 4 21 9" fill="none" strokeWidth={2.5} stroke="#92400e" />
      <path d="M5 11 C5 9 7 8 9 8 H23 C25 8 27 9 27 11 L26 26 C26 27 25 28 24 28 H8 C7 28 6 27 6 26 Z" fill="#b45309" />
      <path d="M6 15 H26" />
      <rect x={13} y={13} width={6} height={5} rx={1} fill="#facc15" />
    </g>
  ),
  clan: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M16 3 L27 7 V15 C27 22 22 26 16 29 C10 26 5 22 5 15 V7 Z" fill="#3b82f6" />
      <path d="M16 3 V29 C10 26 5 22 5 15 V7 Z" fill="#60a5fa" />
      <path d="M16 10 L18 14.5 L23 15 L19.3 18.2 L20.4 23 L16 20.4 L11.6 23 L12.7 18.2 L9 15 L14 14.5 Z" fill="#facc15" />
    </g>
  ),
  chat: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M4 6 H24 C25 6 26 7 26 8 V18 C26 19 25 20 24 20 H12 L7 25 V20 H6 C5 20 4 19 4 18 Z" fill="#e2e8f0" />
      <path d="M28 12 V22 C28 23 27 24 26 24 H25 V28 L21 24 H14" fill="#38bdf8" />
      <circle cx={10} cy={13} r={1.4} fill={OUTLINE} />
      <circle cx={15} cy={13} r={1.4} fill={OUTLINE} />
      <circle cx={20} cy={13} r={1.4} fill={OUTLINE} />
    </g>
  ),
  mail: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <rect x={3} y={8} width={26} height={18} rx={2} fill="#fef3c7" />
      <path d="M3 9 L16 19 L29 9" fill="#fde68a" />
      <path d="M3 26 L12 17 M29 26 L20 17" fill="none" />
      <circle cx={16} cy={19} r={3} fill="#ef4444" />
    </g>
  ),
  rating: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M9 4 H23 V11 C23 16 20 19 16 19 C12 19 9 16 9 11 Z" fill="#facc15" />
      <path d="M9 7 H4 C4 12 6 14 9.5 14 M23 7 H28 C28 12 26 14 22.5 14" fill="none" stroke="#ca8a04" strokeWidth={2} />
      <path d="M14 19 H18 V24 H14 Z" fill="#eab308" />
      <path d="M9 24 H23 V28 H9 Z" fill="#92400e" />
      <path d="M16 7 L17.2 10 L20 10.2 L17.8 12 L18.5 15 L16 13.4 L13.5 15 L14.2 12 L12 10.2 L14.8 10 Z" fill="#fef9c3" />
    </g>
  ),
  more: (
    <g stroke={OUTLINE} strokeWidth={1.5}>
      <rect x={4} y={4} width={10} height={10} rx={2.5} fill="#facc15" />
      <rect x={18} y={4} width={10} height={10} rx={2.5} fill="#38bdf8" />
      <rect x={4} y={18} width={10} height={10} rx={2.5} fill="#4ade80" />
      <rect x={18} y={18} width={10} height={10} rx={2.5} fill="#f472b6" />
    </g>
  ),
};

const RESOURCES: Record<string, ReactElement> = {
  gold: (
    <g stroke={OUTLINE} strokeWidth={1.5}>
      <circle cx={16} cy={16} r={12} fill="#facc15" />
      <circle cx={16} cy={16} r={8} fill="#fde047" stroke="#ca8a04" />
      <path d="M16 11 V21 M13 13.5 H18 A2 2 0 0 1 18 17.5 H14" stroke="#ca8a04" fill="none" />
    </g>
  ),
  wood: (
    <g stroke={OUTLINE} strokeWidth={1.5}>
      <rect x={4} y={9} width={22} height={9} rx={4.5} fill="#b45309" />
      <rect x={7} y={17} width={22} height={9} rx={4.5} fill="#92400e" />
      <ellipse cx={26} cy={13.5} rx={3} ry={4.5} fill="#fcd34d" />
      <ellipse cx={29} cy={21.5} rx={2.5} ry={4.5} fill="#fcd34d" />
    </g>
  ),
  iron: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M3 22 L8 14 H24 L29 22 Z" fill="#94a3b8" />
      <path d="M8 14 L12 8 H22 L24 14 Z" fill="#cbd5e1" />
      <path d="M12 8 L14 14" stroke="#64748b" />
    </g>
  ),
  // М'ясо (ключ food, рішення 20.10.2026): окіст із кісткою
  food: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M20 5 C27 5 29 13 25 18 C22 22 15 23 12 20 C9 17 11 9 14 7 C16 5.5 18 5 20 5 Z" fill="#dc2626" />
      <path d="M17 9 C20 8 24 10 24 13" fill="none" stroke="#fca5a5" strokeWidth={2} />
      <path d="M13 19 L7 25" stroke="#f8fafc" strokeWidth={3.5} strokeLinecap="round" />
      <circle cx={5.5} cy={25} r={2.4} fill="#f8fafc" />
      <circle cx={7.5} cy={27.5} r={2.4} fill="#f8fafc" />
    </g>
  ),
  // Корм звірів: мішок із зерном
  fodder: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M10 8 L22 8 L24 12 C28 16 28 26 24 28 H8 C4 26 4 16 8 12 Z" fill="#d6b77a" />
      <path d="M10 8 L13 4 L16 7 L19 4 L22 8" fill="#a16207" />
      <path d="M9 12 H23" />
      <ellipse cx={16} cy={20} rx={4} ry={3} fill="#facc15" />
    </g>
  ),
};

export function NavIcon({ name, size = 28 }: { name: string; size?: number }) {
  return (
    <svg viewBox="0 0 32 32" width={size} height={size} aria-hidden="true">
      {NAV[name] ?? NAV.more}
    </svg>
  );
}

/** Знак ресурсу; невідомий ресурс — порожньо, назву тоді показує підказка. */
export function ResourceGlyph({ resource, size = 20 }: { resource: string; size?: number }) {
  const glyph = RESOURCES[resource.toLowerCase()];
  if (glyph === undefined) return null;

  return (
    <svg viewBox="0 0 32 32" width={size} height={size} aria-hidden="true">
      {glyph}
    </svg>
  );
}
