import type { ArtResult } from "./buildingArt";
import { at } from "./isoShapes";

interface Props {
  art: ArtResult;
  x: number;
  y: number;
  label: string;
  /** Текст бульбашки збору. null — збирати нічого. */
  bubble: string | null;
  onSelect: () => void;
  onCollect: () => void;
}

/**
 * Підпис і бульбашка будівлі. Малюються після всіх силуетів:
 * інакше ближня будівля перекриває підпис дальньої.
 */
export default function IsoBuildingOverlay({ art, x, y, label, bubble, onSelect, onCollect }: Props) {
  const anchor = at(x, y, art.height);
  const labelPoint = at(x + art.foot, y + art.foot);
  const width = label.length * 7 + 20;

  return (
    <g>
      <g className="cursor-pointer" transform={`translate(${labelPoint.x} ${labelPoint.y + 18})`} onClick={onSelect}>
        <rect x={-width / 2} y={-11} width={width} height={20} rx={10} fill="rgba(15, 23, 42, 0.75)" />
        <text textAnchor="middle" y={4} fontSize={12} fontWeight={600} fill="white">
          {label}
        </text>
      </g>

      {bubble !== null && (
        <g className="cursor-pointer" transform={`translate(${anchor.x} ${anchor.y - 22})`} onClick={onCollect}>
          <rect x={-34} y={-15} width={68} height={28} rx={14} fill="white" stroke="#10b981" strokeWidth={2} />
          <text textAnchor="middle" y={5} fontSize={13} fontWeight={700} fill="#047857">
            {bubble}
          </text>
          <path d="M-6 13 L0 21 L6 13 Z" fill="white" stroke="#10b981" strokeWidth={2} />
        </g>
      )}
    </g>
  );
}
