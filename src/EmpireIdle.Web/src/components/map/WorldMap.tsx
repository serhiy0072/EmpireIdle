import { useEffect, useMemo, useRef } from "react";
import { usePanZoom } from "../../hooks/usePanZoom";
import type { MapAreaResponse, MarchResponse } from "../../lib/apiTypes";
import { at, project, UNIT_X, UNIT_Y } from "../../lib/iso";
import { cellOrigin, groundFill, occupantArt, TILE, tileDetail, tilePath } from "./worldTiles";

interface Props {
  area: MapAreaResponse;
  /** Клітина власного села — прапор і початок ліній маршів. */
  home: { x: number; y: number };
  /** Центр завантаженої ділянки та її радіус — щоб знати, коли підтягнути сусідню. */
  center: { x: number; y: number };
  radius: number;
  marches: MarchResponse[];
  selected: { x: number; y: number } | null;
  /** Скільки разів натиснуто «Додому»: зміна значення повертає камеру на село. */
  homeRequest: number;
  onSelect: (x: number, y: number) => void;
  /** Камера від'їхала від центру ділянки — час завантажити нову навколо цієї клітини. */
  onCenterChange: (x: number, y: number) => void;
}

/** Межі ділянки в пікселях проєкції — кадр для першого показу й для «Додому». */
function boundsOf(center: { x: number; y: number }, radius: number) {
  const from = (center.x - radius - 0.5) * TILE;
  const to = (center.x + radius + 0.5) * TILE;
  const fromY = (center.y - radius - 0.5) * TILE;
  const toY = (center.y + radius + 0.5) * TILE;

  return {
    minX: (from - toY) * UNIT_X,
    maxX: (to - fromY) * UNIT_X,
    minY: (from + fromY) * UNIT_Y - 40,
    maxY: (to + toY) * UNIT_Y,
  };
}

/** Клітина під точкою екрана: обернена ізометрична проєкція. */
function cellAt(px: number, py: number, t: { x: number; y: number; k: number }) {
  const wx = (px - t.x) / t.k;
  const wy = (py - t.y) / t.k;
  const planX = (wx / UNIT_X + wy / UNIT_Y) / 2;
  const planY = (wy / UNIT_Y - wx / UNIT_X) / 2;

  return { x: Math.round(planX / TILE), y: Math.round(planY / TILE) };
}

/**
 * Ізометрична мапа світу — та сама камера й примітиви, що в селі.
 * Ділянка навколо центру приїздить із сервера; коли камера відходить
 * від центру на пів радіуса, сторінка перезапитує ділянку довкола нової
 * клітини, а старі тайли лишаються на місці до приходу нових.
 */
export default function WorldMap({ area, home, center, radius, marches, selected, homeRequest, onSelect, onCenterChange }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const initial = useMemo(() => boundsOf(home, radius), [home, radius]);
  const { transform, handlers, wasDragged, focus } = usePanZoom(containerRef, initial);

  // «Додому» — камера назад на село; перший рендер уже вписаний хуком
  const firstHome = useRef(true);
  useEffect(() => {
    if (firstHome.current) {
      firstHome.current = false;
      return;
    }
    focus(boundsOf(home, radius));
  }, [homeRequest, home, radius, focus]);

  // Стрімінг: після паузи в русі дивимось, яка клітина під центром кадру
  useEffect(() => {
    if (transform === null) return;
    const element = containerRef.current;
    if (element === null) return;

    const timer = window.setTimeout(() => {
      const rect = element.getBoundingClientRect();
      const cell = cellAt(rect.width / 2, rect.height / 2, transform);

      if (Math.max(Math.abs(cell.x - center.x), Math.abs(cell.y - center.y)) > radius / 2) {
        onCenterChange(cell.x, cell.y);
      }
    }, 250);

    return () => window.clearTimeout(timer);
  }, [transform, center, radius, onCenterChange]);

  // Від дальніх до ближніх: пагорб чи дерево не має проступати крізь ближчий тайл
  const terrain = useMemo(() => [...area.terrain].sort((a, b) => a.x + a.y - (b.x + b.y) || a.x - b.x), [area.terrain]);
  const occupants = useMemo(() => new Map(area.occupants.map((o) => [`${o.x}:${o.y}`, o])), [area.occupants]);

  const tap = (x: number, y: number) => () => {
    if (!wasDragged()) onSelect(x, y);
  };

  const showLabels = transform !== null && transform.k > 1.1;
  const inside = (x: number, y: number) => x >= area.minX && x <= area.maxX && y >= area.minY && y <= area.maxY;

  return (
    <div
      ref={containerRef}
      {...handlers}
      role="img"
      aria-label="Мапа світу"
      className="relative aspect-square w-full touch-none select-none overflow-hidden rounded-xl bg-gradient-to-b from-sky-200 to-sky-50 lg:aspect-auto lg:h-[640px]"
    >
      {transform !== null && (
        <svg className="absolute inset-0 h-full w-full">
          <g transform={`translate(${transform.x} ${transform.y}) scale(${transform.k})`}>
            {/* Основи всіх клітин — одним шаром, деталі окремо, інакше дерево далекої клітини лягало б поверх ближньої основи */}
            {terrain.map((cell) => (
              <path
                key={`g${cell.x}:${cell.y}`}
                d={tilePath(cell.x, cell.y)}
                fill={groundFill(cell.type)}
                stroke="rgba(15, 23, 42, 0.06)"
                strokeWidth={0.5}
                className="cursor-pointer"
                onClick={tap(cell.x, cell.y)}
              />
            ))}

            {marches.map((march) => {
              if (!inside(march.targetX, march.targetY)) return null;
              const from = at(cellOrigin(home.x, home.y).x, cellOrigin(home.x, home.y).y, 4);
              const to = at(cellOrigin(march.targetX, march.targetY).x, cellOrigin(march.targetX, march.targetY).y, 4);

              return (
                <line
                  key={march.id}
                  x1={from.x}
                  y1={from.y}
                  x2={to.x}
                  y2={to.y}
                  stroke="#0f172a"
                  strokeWidth={1.5}
                  strokeDasharray="5 4"
                  opacity={0.6}
                  pointerEvents="none"
                />
              );
            })}

            {terrain.map((cell) => {
              const occupant = occupants.get(`${cell.x}:${cell.y}`);
              const isHome = cell.x === home.x && cell.y === home.y;
              const detail = occupant === undefined ? tileDetail(cell.type, cell.x, cell.y) : null;

              if (occupant === undefined && detail === null) return null;

              return (
                <g key={`d${cell.x}:${cell.y}`} className="cursor-pointer" onClick={tap(cell.x, cell.y)}>
                  {occupant === undefined ? detail : occupantArt(occupant.occupantType, cell.x, cell.y, isHome)}
                </g>
              );
            })}

            {selected !== null && inside(selected.x, selected.y) && (
              <path d={tilePath(selected.x, selected.y)} fill="rgba(16, 185, 129, 0.25)" stroke="#10b981" strokeWidth={1.5} pointerEvents="none" />
            )}

            {showLabels &&
              area.occupants.map((occupant) => {
                const origin = cellOrigin(occupant.x, occupant.y);
                const p = project(origin.x, origin.y);
                const isHome = occupant.x === home.x && occupant.y === home.y;
                const text = isHome ? "Ваше село" : occupant.occupantType === "Monster" ? `☠ ${occupant.name ?? "Монстр"}` : (occupant.name ?? "Село");
                const width = text.length * 5.5 + 12;

                return (
                  <g key={`l${occupant.x}:${occupant.y}`} transform={`translate(${p.x} ${p.y + 14})`} pointerEvents="none">
                    <rect x={-width / 2} y={-8} width={width} height={14} rx={7} fill="rgba(15, 23, 42, 0.7)" />
                    <text textAnchor="middle" y={3} fontSize={9} fontWeight={600} fill="white">
                      {text}
                    </text>
                  </g>
                );
              })}
          </g>
        </svg>
      )}
    </div>
  );
}
