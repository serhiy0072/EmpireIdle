import type { ReactElement } from "react";
import { rarityKey } from "../../lib/rarity";

/**
 * Іконка предмета: процедурний SVG за ключем і типом, рамка за рідкістю.
 * Ключ підказує форму (sword, bow, staff, amulet…), тип — запасний варіант
 * для незнайомого ключа. Справжній арт підключиться через маніфест, як у героїв.
 */
interface Props {
  itemKey: string;
  type: string;
  rarity: string | number | undefined;
  size?: number;
  className?: string;
}

const FRAME: Record<string, { stroke: string; back: string }> = {
  Common: { stroke: "#94a3b8", back: "#f1f5f9" },
  Rare: { stroke: "#38bdf8", back: "#e0f2fe" },
  Unique: { stroke: "#f59e0b", back: "#fef3c7" },
};

const SHAPES: { test: RegExp; draw: () => ReactElement }[] = [
  {
    test: /sword|blade/,
    draw: () => (
      <g>
        <path d="M30 70 L66 34" stroke="#cbd5e1" strokeWidth={9} strokeLinecap="round" />
        <path d="M30 70 L66 34" stroke="#f8fafc" strokeWidth={3} strokeLinecap="round" />
        <path d="M24 60 L40 76" stroke="#b45309" strokeWidth={6} strokeLinecap="round" />
        <circle cx={22} cy={78} r={4} fill="#78350f" />
      </g>
    ),
  },
  {
    test: /lance|spear|pike/,
    draw: () => (
      <g>
        <path d="M24 76 L64 36" stroke="#78350f" strokeWidth={5} strokeLinecap="round" />
        <path d="M60 40 L78 22 L72 44 Z" fill="#cbd5e1" stroke="#475569" strokeWidth={1.5} />
      </g>
    ),
  },
  {
    test: /bow|arrow/,
    draw: () => (
      <g>
        <path d="M34 22 Q76 50 34 78" fill="none" stroke="#78350f" strokeWidth={5} strokeLinecap="round" />
        <path d="M34 22 L34 78" stroke="#e5e7eb" strokeWidth={1.5} />
        <path d="M34 50 L70 50" stroke="#475569" strokeWidth={2.5} />
        <path d="M70 50 L62 44 M70 50 L62 56" stroke="#475569" strokeWidth={2.5} strokeLinecap="round" />
      </g>
    ),
  },
  {
    test: /staff|wand|rod/,
    draw: () => (
      <g>
        <path d="M32 78 L60 30" stroke="#78350f" strokeWidth={5} strokeLinecap="round" />
        <circle cx={62} cy={26} r={9} fill="#fb923c" opacity={0.9} />
        <circle cx={62} cy={26} r={4} fill="#fef3c7" />
      </g>
    ),
  },
  {
    test: /amulet|pendant/,
    draw: () => (
      <g>
        <path d="M30 22 Q50 60 70 22" fill="none" stroke="#b45309" strokeWidth={3} />
        <path d="M50 44 L62 58 L50 76 L38 58 Z" fill="#a78bfa" stroke="#5b21b6" strokeWidth={1.5} />
      </g>
    ),
  },
  {
    test: /ring/,
    draw: () => (
      <g>
        <circle cx={50} cy={56} r={18} fill="none" stroke="#fbbf24" strokeWidth={7} />
        <circle cx={50} cy={34} r={7} fill="#f87171" stroke="#991b1b" strokeWidth={1.5} />
      </g>
    ),
  },
  {
    test: /sigil|seal|rune/,
    draw: () => (
      <g>
        <circle cx={50} cy={50} r={24} fill="#c4b5fd" stroke="#5b21b6" strokeWidth={2} />
        <path d="M38 62 L50 34 L62 62 M42 54 L58 54" fill="none" stroke="#3b0764" strokeWidth={3} strokeLinecap="round" />
      </g>
    ),
  },
  {
    test: /chime|bell/,
    draw: () => (
      <g>
        <path d="M34 62 Q34 30 50 26 Q66 30 66 62 Z" fill="#fbbf24" stroke="#b45309" strokeWidth={2} />
        <rect x={30} y={62} width={40} height={6} rx={3} fill="#b45309" />
        <circle cx={50} cy={74} r={4} fill="#78350f" />
      </g>
    ),
  },
  {
    test: /essence/,
    draw: () => (
      <g>
        <path d="M50 22 Q70 44 62 62 Q56 74 50 78 Q44 74 38 62 Q30 44 50 22 Z" fill="#a78bfa" stroke="#5b21b6" strokeWidth={2} />
        <path d="M50 42 Q58 52 54 62 Q52 68 50 70 Q48 68 46 62 Q42 52 50 42 Z" fill="#fef3c7" />
      </g>
    ),
  },
  {
    test: /crate|chest|box/,
    draw: () => (
      <g>
        <path d="M26 42 L50 30 L74 42 L74 70 L50 82 L26 70 Z" fill="#d6b98c" stroke="#78350f" strokeWidth={2} />
        <path d="M50 54 L74 42 M50 54 L26 42 M50 54 L50 82" stroke="#78350f" strokeWidth={2} />
      </g>
    ),
  },
  {
    test: /teleport|portal/,
    draw: () => (
      <g>
        <ellipse cx={50} cy={50} rx={16} ry={26} fill="#7dd3fc" stroke="#0369a1" strokeWidth={3} />
        <ellipse cx={50} cy={50} rx={7} ry={14} fill="#e0f2fe" />
      </g>
    ),
  },
  {
    test: /attack|war/,
    draw: () => (
      <g>
        <path d="M30 30 L70 70 M70 30 L30 70" stroke="#dc2626" strokeWidth={7} strokeLinecap="round" />
      </g>
    ),
  },
  {
    test: /defense|fortif|shield/,
    draw: () => (
      <g>
        <path d="M50 22 L72 32 L68 60 Q60 74 50 80 Q40 74 32 60 L28 32 Z" fill="#60a5fa" stroke="#1e40af" strokeWidth={2} />
      </g>
    ),
  },
  {
    test: /boost|production|rush/,
    draw: () => (
      <g>
        <path d="M50 24 L70 48 L58 48 L58 76 L42 76 L42 48 L30 48 Z" fill="#4ade80" stroke="#15803d" strokeWidth={2} />
      </g>
    ),
  },
];

const BY_TYPE: Record<string, RegExp> = {
  equipment: /sword/,
  boost: /boost/,
  resources: /crate/,
  teleport: /teleport/,
  evolution: /essence/,
};

export default function ItemIcon({ itemKey, type, rarity, size = 48, className = "" }: Props): ReactElement {
  const frame = FRAME[rarityKey(rarity)] ?? (FRAME.Common as { stroke: string; back: string });
  const key = itemKey.toLowerCase();
  const shape = SHAPES.find((candidate) => candidate.test.test(key)) ?? SHAPES.find((candidate) => candidate.test.test(BY_TYPE[type]?.source ?? ""));

  return (
    <svg viewBox="0 0 100 100" width={size} height={size} className={`shrink-0 rounded-lg ${className}`} aria-hidden>
      <rect width={100} height={100} rx={12} fill={frame.back} />
      {shape?.draw()}
      <rect x={2} y={2} width={96} height={96} rx={11} fill="none" stroke={frame.stroke} strokeWidth={3} />
    </svg>
  );
}
