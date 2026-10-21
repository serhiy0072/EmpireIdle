import { tierFrame } from "../../lib/unitTiers";

/**
 * Намальовані юніти табору (референс 13.10.2026): моделей у грі немає, тож фігура збирається з SVG —
 * колір одягу від типу юніта, обладунок і рамка від рівня. Нові типи без малюнка отримують піхотинця.
 */

interface Look {
  cloth: string;
  trim: string;
}

const INFANTRY: Look = { cloth: "#4d7c0f", trim: "#365314" };

const LOOKS: Record<string, Look> = {
  infantry: INFANTRY,
  spearman: { cloth: "#1d4ed8", trim: "#1e3a8a" },
  archer: { cloth: "#ca8a04", trim: "#854d0e" },
};

/** Обладунок темніє й важчає з рівнем: шкіра → залізо → сталь. */
function armour(level: number): { plate: string; edge: string } {
  if (level >= 7) return { plate: "#475569", edge: "#1e293b" };
  if (level >= 4) return { plate: "#94a3b8", edge: "#475569" };
  return { plate: "#a16207", edge: "#713f12" };
}

function Weapon({ unitType, edge }: { unitType: string; edge: string }) {
  if (unitType === "spearman") {
    return (
      <g>
        <line x1="78" y1="18" x2="78" y2="112" stroke="#78350f" strokeWidth="3.5" strokeLinecap="round" />
        <path d="M78 6 L84 22 L78 19 L72 22 Z" fill="#e2e8f0" stroke={edge} strokeWidth="1.2" />
      </g>
    );
  }

  if (unitType === "archer") {
    return (
      <g fill="none" strokeLinecap="round">
        <path d="M80 30 Q98 62 80 94" stroke="#78350f" strokeWidth="3.5" />
        <line x1="80" y1="30" x2="80" y2="94" stroke="#e2e8f0" strokeWidth="1" />
        <line x1="62" y1="62" x2="92" y2="62" stroke="#78350f" strokeWidth="2" />
        <path d="M92 62 L86 58 M92 62 L86 66" stroke="#e2e8f0" strokeWidth="2" />
      </g>
    );
  }

  // Піхота: щит і меч
  return (
    <g>
      <line x1="80" y1="40" x2="80" y2="84" stroke="#e2e8f0" strokeWidth="4" strokeLinecap="round" />
      <line x1="73" y1="80" x2="87" y2="80" stroke={edge} strokeWidth="3.5" strokeLinecap="round" />
      <path d="M22 56 L42 52 L42 86 Q32 96 22 86 Z" fill="#64748b" stroke={edge} strokeWidth="2" />
      <path d="M32 58 L32 88" stroke="#e2e8f0" strokeWidth="1.5" opacity="0.6" />
    </g>
  );
}

/** Фігура юніта: на великій сцені табору й мініатюрою в плитці рівня. */
export function UnitFigure({ unitType, level, size }: { unitType: string; level: number; size: number }) {
  const look = LOOKS[unitType] ?? INFANTRY;
  const { plate, edge } = armour(level);
  const frame = tierFrame(level);

  return (
    <svg viewBox="0 0 100 120" width={size} height={size * 1.2} aria-hidden>
      {/* Ноги й тулуб */}
      <rect x="40" y="88" width="8" height="24" rx="3" fill={edge} />
      <rect x="52" y="88" width="8" height="24" rx="3" fill={edge} />
      <path d="M34 50 Q50 42 66 50 L64 92 L36 92 Z" fill={look.cloth} stroke={look.trim} strokeWidth="2" />
      <path d="M38 52 Q50 46 62 52 L60 76 L40 76 Z" fill={plate} stroke={edge} strokeWidth="1.5" />
      {/* Пояс із пряжкою кольору рівня */}
      <rect x="36" y="76" width="28" height="5" fill={look.trim} />
      <circle cx="50" cy="78.5" r="3" fill={frame.from} stroke={edge} strokeWidth="1" />
      {/* Наплічники */}
      <ellipse cx="34" cy="53" rx="8" ry="6" fill={plate} stroke={edge} strokeWidth="1.5" />
      <ellipse cx="66" cy="53" rx="8" ry="6" fill={plate} stroke={edge} strokeWidth="1.5" />
      {/* Руки */}
      <rect x="27" y="56" width="7" height="24" rx="3" fill={look.cloth} stroke={look.trim} strokeWidth="1.5" />
      <rect x="66" y="56" width="7" height="24" rx="3" fill={look.cloth} stroke={look.trim} strokeWidth="1.5" />
      {/* Голова в шоломі з прорізом */}
      <path d="M38 34 Q38 16 50 16 Q62 16 62 34 L62 44 L38 44 Z" fill={plate} stroke={edge} strokeWidth="2" />
      <rect x="42" y="30" width="16" height="3.5" rx="1" fill="#0f172a" />
      {/* Плюмаж — з VII рівня */}
      {level >= 7 && <path d="M50 16 Q58 4 66 10 Q58 10 54 18 Z" fill={frame.from} stroke={frame.to} strokeWidth="1" />}
      <Weapon unitType={unitType} edge={edge} />
    </svg>
  );
}
