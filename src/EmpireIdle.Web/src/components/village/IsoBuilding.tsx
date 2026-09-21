import { toPath } from "../../lib/iso";
import { artFor } from "./buildingArt";
import { at } from "./isoShapes";

interface Props {
  buildingKey: string;
  name: string;
  x: number;
  y: number;
  level: number;
  selected: boolean;
  underConstruction: boolean;
  /** Текст бульбашки збору. null — збирати нічого. */
  bubble: string | null;
  onSelect: () => void;
  onCollect: () => void;
}

/** Будівля трохи росте з рівнем, але не безмежно — інакше закриє сусідів. */
function scale(level: number): number {
  return 1 + Math.min(level, 30) * 0.015;
}

export default function IsoBuilding({ buildingKey, name, x, y, level, selected, underConstruction, bubble, onSelect, onCollect }: Props) {
  const art = artFor(buildingKey)(x, y, scale(level));
  const f = art.foot + 0.8;

  const ground = [at(x - f, y - f), at(x + f, y - f), at(x + f, y + f), at(x - f, y + f)];
  const anchor = at(x, y, art.height);
  const labelPoint = at(x + art.foot, y + art.foot);

  const text = `${name} · ${level}`;
  const width = text.length * 7 + 20;

  return (
    <g className="cursor-pointer" onClick={onSelect}>
      {/* Підсвітка вибраної або риштування будови — на землі, під силуетом */}
      {(selected || underConstruction) && (
        <path
          d={toPath(ground)}
          fill={selected ? "rgba(16, 185, 129, 0.25)" : "none"}
          stroke={selected ? "#10b981" : "#f59e0b"}
          strokeWidth={3}
          strokeDasharray={underConstruction ? "10 6" : undefined}
        />
      )}

      <g opacity={underConstruction ? 0.55 : 1}>{art.node}</g>

      <g transform={`translate(${labelPoint.x} ${labelPoint.y + 18})`}>
        <rect x={-width / 2} y={-11} width={width} height={20} rx={10} fill="rgba(15, 23, 42, 0.75)" />
        <text textAnchor="middle" y={4} fontSize={12} fontWeight={600} fill="white">
          {text}
        </text>
      </g>

      {bubble !== null && (
        <g
          transform={`translate(${anchor.x} ${anchor.y - 22})`}
          onClick={(event) => {
            // Збір не має заодно вибирати будівлю
            event.stopPropagation();
            onCollect();
          }}
        >
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
