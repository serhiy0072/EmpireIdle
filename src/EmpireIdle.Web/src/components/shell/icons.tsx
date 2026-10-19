import type { ReactElement } from "react";

/**
 * Значки ігрової оболонки — процедурні SVG у полі 0..32, як решта арту гри: нижня панель розділів
 * і ресурси в шапці. Контур темний, заливка яскрава — читаються і на синьому тлі, і на світлому.
 */

const OUTLINE = "#0f2147";

const NAV: Record<string, ReactElement> = {
  village: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M4 15 L11 8 L18 15 Z" fill="#ef4444" />
      <rect x={6} y={15} width={10} height={10} fill="#fde68a" />
      <path d="M15 12 L22 5 L29 12 Z" fill="#3b82f6" />
      <rect x={17} y={12} width={10} height={13} fill="#e2e8f0" />
      <rect x={9.5} y={19} width={3} height={6} fill="#92400e" />
      <rect x={20.5} y={16} width={3} height={3} fill="#38bdf8" />
      <path d="M2 26 H30" />
    </g>
  ),
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
  food: (
    <g stroke={OUTLINE} strokeWidth={1.5} strokeLinejoin="round">
      <path d="M16 29 V8" stroke="#a16207" strokeWidth={2} />
      {[9, 14, 19].map((y) => (
        <g key={y}>
          <ellipse cx={12.5} cy={y} rx={3.5} ry={2} transform={`rotate(-35 12.5 ${y})`} fill="#facc15" />
          <ellipse cx={19.5} cy={y} rx={3.5} ry={2} transform={`rotate(35 19.5 ${y})`} fill="#facc15" />
        </g>
      ))}
      <ellipse cx={16} cy={5} rx={2} ry={3} fill="#facc15" />
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
