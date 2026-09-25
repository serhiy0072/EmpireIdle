import { useEffect, useState } from "react";
import { at } from "../../lib/iso";
import { formatRemaining } from "../../lib/time";
import { cellOrigin } from "./worldTiles";

/** Один марш на мапі: звідки, куди й коли. departedAt null — старт ноги невідомий, загін не рухаємо. */
export interface MapMarch {
  id: string;
  fromX: number;
  fromY: number;
  toX: number;
  toY: number;
  departedAt: string | null;
  arrivesAt: string;
  /** Ворожий марш на своїх — червоний; власний — темний. */
  hostile: boolean;
}

interface Props {
  marches: MapMarch[];
}

/** Висота лінії й загону над землею: щоб не губились у деталях клітин. */
const Z = 4;

/**
 * Марші на мапі: пряма від поселення до цілі, по ній — загін, над ним зворотний відлік.
 * Кадр тикає лише цей шар: мапа під ним важка й перемальовуватись щокадру не повинна.
 */
export default function MarchAnimation({ marches }: Props) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (marches.length === 0) return;

    let frame = 0;
    let last = 0;

    // ~20 кадрів на секунду: загін повзе плавно, а React не перераховує шар 60 разів
    const tick = (time: number) => {
      if (time - last > 50) {
        last = time;
        setNow(Date.now());
      }
      frame = requestAnimationFrame(tick);
    };

    frame = requestAnimationFrame(tick);

    return () => cancelAnimationFrame(frame);
  }, [marches.length]);

  return (
    <g pointerEvents="none">
      {marches.map((march) => {
        const fromCell = cellOrigin(march.fromX, march.fromY);
        const toCell = cellOrigin(march.toX, march.toY);
        const from = at(fromCell.x, fromCell.y, Z);
        const to = at(toCell.x, toCell.y, Z);

        const arrives = Date.parse(march.arrivesAt);
        const departs = march.departedAt === null ? null : Date.parse(march.departedAt);
        const progress =
          departs === null || arrives <= departs ? null : Math.min(Math.max((now - departs) / (arrives - departs), 0), 1);

        // Без відомого старту відлік висить посередині лінії
        const share = progress ?? 0.5;
        const army = { x: from.x + (to.x - from.x) * share, y: from.y + (to.y - from.y) * share };
        const color = march.hostile ? "#dc2626" : "#0f172a";
        const label = formatRemaining(march.arrivesAt, now);
        const width = label.length * 5.5 + 12;

        return (
          <g key={march.id}>
            <line
              x1={from.x}
              y1={from.y}
              x2={to.x}
              y2={to.y}
              stroke={color}
              strokeWidth={1.5}
              strokeDasharray="5 4"
              opacity={0.6}
            />
            {progress !== null && (
              <g transform={`translate(${army.x} ${army.y})`}>
                <circle r={4.5} fill={color} stroke="white" strokeWidth={1.2} />
                <path d="M -2 1.5 L 2 -2.5 M -2 -2.5 L 2 1.5" stroke="white" strokeWidth={1.1} strokeLinecap="round" />
              </g>
            )}
            <g transform={`translate(${army.x} ${army.y - 12})`}>
              <rect x={-width / 2} y={-8} width={width} height={14} rx={7} fill={color} opacity={0.85} />
              <text textAnchor="middle" y={3} fontSize={9} fontWeight={600} fill="white">
                {label}
              </text>
            </g>
          </g>
        );
      })}
    </g>
  );
}
