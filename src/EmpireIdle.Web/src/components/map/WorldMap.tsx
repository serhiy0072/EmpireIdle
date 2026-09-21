import type { MapAreaResponse, MarchResponse } from "../../lib/apiTypes";
import { terrainFill } from "../../lib/terrain";

/** Розмір клітини в одиницях viewBox: мапа масштабується під ширину контейнера. */
const CELL = 24;

interface Props {
  area: MapAreaResponse;
  /** Клітина власного села — зірка й початок ліній маршів. */
  home: { x: number; y: number };
  marches: MarchResponse[];
  selected: { x: number; y: number } | null;
  onSelect: (x: number, y: number) => void;
}

/**
 * Сітка світу навколо гравця. Окупанти — іконками без імен: ім'я приїде
 * з деталей клітини, а тут воно не влізе. Марші — пунктиром від дому.
 */
export default function WorldMap({ area, home, marches, selected, onSelect }: Props) {
  const width = (area.maxX - area.minX + 1) * CELL;
  const height = (area.maxY - area.minY + 1) * CELL;

  const px = (x: number) => (x - area.minX) * CELL;
  const py = (y: number) => (y - area.minY) * CELL;
  const inside = (x: number, y: number) => x >= area.minX && x <= area.maxX && y >= area.minY && y <= area.maxY;

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      className="h-auto w-full select-none rounded-xl border border-slate-200 bg-slate-100"
      role="img"
      aria-label="Мапа світу"
    >
      {area.terrain.map((cell) => (
        <rect
          key={`${cell.x}:${cell.y}`}
          x={px(cell.x)}
          y={py(cell.y)}
          width={CELL}
          height={CELL}
          fill={terrainFill(cell.type)}
          stroke="rgba(15, 23, 42, 0.08)"
          className="cursor-pointer"
          onClick={() => onSelect(cell.x, cell.y)}
        />
      ))}

      {marches.map((march) => {
        // Лінія завжди між домом і ціллю: напрямок каже стан, а не геометрія
        if (!inside(march.targetX, march.targetY)) return null;

        return (
          <line
            key={march.id}
            x1={px(home.x) + CELL / 2}
            y1={py(home.y) + CELL / 2}
            x2={px(march.targetX) + CELL / 2}
            y2={py(march.targetY) + CELL / 2}
            stroke="#0f172a"
            strokeWidth={2}
            strokeDasharray="6 4"
            opacity={0.6}
            pointerEvents="none"
          />
        );
      })}

      {area.occupants.map((occupant) => {
        const cx = px(occupant.x) + CELL / 2;
        const cy = py(occupant.y) + CELL / 2;
        const isHome = occupant.x === home.x && occupant.y === home.y;

        return (
          <g
            key={`${occupant.x}:${occupant.y}`}
            className="cursor-pointer"
            onClick={() => onSelect(occupant.x, occupant.y)}
          >
            {occupant.occupantType === "Monster" ? (
              <>
                <circle cx={cx} cy={cy} r={CELL * 0.38} fill="#7f1d1d" stroke="#fecaca" strokeWidth={1.5} />
                <text x={cx} y={cy + 4} textAnchor="middle" fontSize={12} fill="white" pointerEvents="none">
                  ☠
                </text>
              </>
            ) : (
              <>
                <rect
                  x={cx - CELL * 0.36}
                  y={cy - CELL * 0.36}
                  width={CELL * 0.72}
                  height={CELL * 0.72}
                  rx={4}
                  fill={isHome ? "#f59e0b" : "#1d4ed8"}
                  stroke="white"
                  strokeWidth={1.5}
                />
                <text x={cx} y={cy + 4} textAnchor="middle" fontSize={12} fill="white" pointerEvents="none">
                  {isHome ? "★" : "⌂"}
                </text>
              </>
            )}
          </g>
        );
      })}

      {selected !== null && inside(selected.x, selected.y) && (
        <rect
          x={px(selected.x)}
          y={py(selected.y)}
          width={CELL}
          height={CELL}
          fill="none"
          stroke="#10b981"
          strokeWidth={3}
          pointerEvents="none"
        />
      )}
    </svg>
  );
}
