import { useEffect, useMemo, useRef, useState } from "react";
import { usePanZoom } from "../../hooks/usePanZoom";
import type { MapAreaResponse } from "../../lib/apiTypes";
import { at, project, toPath, UNIT_X, UNIT_Y } from "../../lib/iso";
import MarchAnimation, { type MapMarch } from "./MarchAnimation";
import { MAP_RADIUS_MAX, MAP_RADIUS_MIN, type MapView } from "../../lib/queries/map";
import { areaPath, cellOrigin, groundFill, isStructureBuilding, occupantArt, TILE, tileDetail, tilePath } from "./worldTiles";

interface Props {
  area: MapAreaResponse;
  /** Клітина власного села — прапор і початок ліній маршів. */
  home: { x: number; y: number };
  /** Завантажена ділянка: центр і радіус — щоб знати, коли підтягнути іншу. */
  view: MapView;
  /** Марші на мапі — власні й ворожі на своїх: лінія, загін і відлік. */
  marches: MapMarch[];
  selected: { x: number; y: number } | null;
  /** Скільки разів натиснуто «Додому»: зміна значення повертає камеру на село. */
  homeRequest: number;
  /** Клітина, на яку перевести камеру (з тривоги); n міняється на кожен запит, навіть на ту саму клітину. */
  focusRequest: { x: number; y: number; n: number } | null;
  /** Сторона світу в клітинах — з каталогу; земля малюється до цього краю й далі камера не їде. */
  mapSize: number;
  /** Клан гравця: його споруди — зелені, з підсвіченою зоною дії; null — поза кланом. */
  ownClanId: string | null;
  /** Радіус зони дії споруди; 0 — території у світі немає, зону не малюємо. */
  coverageRadius: number;
  onSelect: (x: number, y: number) => void;
  /** Камера показує інші клітини — час завантажити ділянку під них. */
  onViewChange: (view: MapView) => void;
}

/** Радіус для першого показу: стільки клітин довкола дому в кадрі. */
const HOME_RADIUS = 12;

/** Скільки клітин довкола центру має вміщати кадр щонайбільше: запас під стелю API (25), щоб край ділянки не показувався. */
const VIEW_RADIUS_CAP = MAP_RADIUS_MAX - 3;

/** Увесь світ у пікселях проєкції: камера не виїжджає за його край, а під незавантаженими клітинами лежить земля. */
function worldBounds(size: number) {
  return {
    minX: -size * TILE * UNIT_X,
    maxX: size * TILE * UNIT_X,
    minY: -TILE * UNIT_Y,
    maxY: 2 * size * TILE * UNIT_Y,
  };
}

/**
 * Масштаб, за якого кадр показує рівно VIEW_RADIUS_CAP клітин довкола центру —
 * обернена radiusFor: далі віддалятись не можна, бо сервер більше не віддасть.
 */
function minScaleFor(viewport: { width: number; height: number }): number {
  const span = (VIEW_RADIUS_CAP - 2) * 2 * TILE;
  return (viewport.width / UNIT_X + viewport.height / UNIT_Y) / 2 / span;
}

const ZOOM_MIN = 0.2;
const ZOOM_MAX = 6;

/** Понад цей радіус дрібні деталі тайлів не малюємо: тисячі дерев не потрібні на огляді регіону. */
const DETAIL_RADIUS = 16;

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
 * Скільки клітин довкола центру вміщає кадр за цього масштабу. Кадр у плані —
 * повернутий прямокутник, його охоплення по кожній осі плану — півсума проєкцій.
 * Квантуємо кроком, щоб плавний зум не породжував запит на кожен тік колеса.
 */
function radiusFor(width: number, height: number, k: number): number {
  const span = (width / k / UNIT_X + height / k / UNIT_Y) / 2 / TILE;
  const step = 4;
  const raw = Math.ceil(span / 2) + 2;

  return Math.min(MAP_RADIUS_MAX, Math.max(MAP_RADIUS_MIN, Math.ceil(raw / step) * step));
}

/**
 * Ізометрична мапа світу — та сама камера й примітиви, що в селі.
 * Ділянка навколо центру приїздить із сервера; її радіус залежить від масштабу:
 * наблизив — менше клітин, віддалив — більше. Коли камера показує інші клітини,
 * сторінка перезапитує ділянку, а старі тайли лишаються до приходу нових.
 */
export default function WorldMap({
  area,
  home,
  view,
  marches,
  selected,
  homeRequest,
  focusRequest,
  mapSize,
  ownClanId,
  coverageRadius,
  onSelect,
  onViewChange,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const initial = useMemo(() => boundsOf(home, HOME_RADIUS), [home]);
  const limits = useMemo(() => ({ min: ZOOM_MIN, max: ZOOM_MAX, minAbsolute: minScaleFor, pan: worldBounds(mapSize) }), [mapSize]);
  const { transform, handlers, wasDragged, focus } = usePanZoom(containerRef, initial, limits);

  // «Додому» — камера назад на село; перший рендер уже вписаний хуком
  const firstHome = useRef(true);
  useEffect(() => {
    if (firstHome.current) {
      firstHome.current = false;
      return;
    }
    focus(boundsOf(home, HOME_RADIUS));
  }, [homeRequest, home, focus]);

  // Тривога веде до цілі нападу: камера туди, решту робить сторінка (виділення клітини)
  useEffect(() => {
    if (focusRequest === null) return;
    focus(boundsOf(focusRequest, HOME_RADIUS));
  }, [focusRequest, focus]);

  // Стрімінг: після паузи в русі чи зумі дивимось, що під кадром, і просимо відповідну ділянку
  useEffect(() => {
    if (transform === null) return;
    const element = containerRef.current;
    if (element === null) return;

    const timer = window.setTimeout(() => {
      const rect = element.getBoundingClientRect();
      const cell = cellAt(rect.width / 2, rect.height / 2, transform);
      const radius = radiusFor(rect.width, rect.height, transform.k);
      const drift = Math.max(Math.abs(cell.x - view.x), Math.abs(cell.y - view.y));

      // Зсув на третину радіуса — ще в межах завантаженого, далі край ділянки вже в кадрі
      if (radius !== view.radius || drift > view.radius / 3) {
        onViewChange({ x: cell.x, y: cell.y, radius });
      }
    }, 250);

    return () => window.clearTimeout(timer);
  }, [transform, view, onViewChange]);

  // Від дальніх до ближніх: пагорб чи дерево не має проступати крізь ближчий тайл
  const terrain = useMemo(() => [...area.terrain].sort((a, b) => a.x + a.y - (b.x + b.y) || a.x - b.x), [area.terrain]);
  const occupants = useMemo(() => new Map(area.occupants.map((o) => [`${o.x}:${o.y}`, o])), [area.occupants]);
  // Годинник без тіку щосекунди — мапа важка: перемальовуємось лише тоді,
  // коли найближча споруда на ділянці добудовується і стає активною
  const [now, setNow] = useState(() => Date.now());
  const nextReady = useMemo(
    () =>
      Math.min(
        ...area.occupants
          .map((o) => (o.readyAt == null ? Infinity : Date.parse(o.readyAt)))
          .filter((at) => at > now),
      ),
    [area.occupants, now],
  );

  useEffect(() => {
    if (!Number.isFinite(nextReady)) return;

    const timer = window.setTimeout(() => setNow(Date.now()), Math.max(nextReady - Date.now(), 0) + 500);

    return () => window.clearTimeout(timer);
  }, [nextReady]);

  const coverage =
    ownClanId === null || coverageRadius <= 0
      ? []
      : area.occupants.filter(
          (o) => o.occupantType === "ClanStructure" && o.clanId === ownClanId && !isStructureBuilding(o, now),
        );
  const loadedRadius = Math.round((area.maxX - area.minX) / 2);
  const detailed = loadedRadius <= DETAIL_RADIUS;

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
            {/* Земля під усім світом: незавантажені клітини — рівнина, а не небо, і краю сітки не видно */}
            <path
              d={toPath([at(0, 0), at(mapSize * TILE, 0), at(mapSize * TILE, mapSize * TILE), at(0, mapSize * TILE)])}
              fill="#bfd9a0"
            />

            {/* Основи всіх клітин — одним шаром, деталі окремо, інакше дерево далекої клітини лягало б поверх ближчої основи */}
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

            {coverage.map((structure) => (
              <path
                key={`c${structure.x}:${structure.y}`}
                d={areaPath(structure.x, structure.y, coverageRadius)}
                fill="rgba(16, 185, 129, 0.1)"
                stroke="rgba(5, 150, 105, 0.5)"
                strokeWidth={1}
                strokeDasharray="4 3"
                pointerEvents="none"
              />
            ))}

            {terrain.map((cell) => {
              const occupant = occupants.get(`${cell.x}:${cell.y}`);
              const isHome = cell.x === home.x && cell.y === home.y;
              const detail = occupant === undefined && detailed ? tileDetail(cell.type, cell.x, cell.y) : null;

              if (occupant === undefined && detail === null) return null;

              return (
                <g key={`d${cell.x}:${cell.y}`} className="cursor-pointer" onClick={tap(cell.x, cell.y)}>
                  {occupant === undefined ? detail : occupantArt(occupant, isHome, ownClanId, now)}
                </g>
              );
            })}

            <MarchAnimation marches={marches} />

            {selected !== null && inside(selected.x, selected.y) && (
              <path d={tilePath(selected.x, selected.y)} fill="rgba(16, 185, 129, 0.25)" stroke="#10b981" strokeWidth={1.5} pointerEvents="none" />
            )}

            {showLabels &&
              area.occupants.map((occupant) => {
                const origin = cellOrigin(occupant.x, occupant.y);
                const p = project(origin.x, origin.y);
                const isHome = occupant.x === home.x && occupant.y === home.y;
                const text = isHome
                  ? "Ваше село"
                  : occupant.occupantType === "Monster"
                    ? `${occupant.name ?? "Монстр"}${occupant.monsterLevel == null ? "" : ` · ${occupant.monsterLevel}`}`
                    : occupant.occupantType === "ClanStructure"
                      ? `[${occupant.name ?? "?"}]${isStructureBuilding(occupant, now) ? " · будується" : ""}`
                      : (occupant.name ?? "Село");
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
