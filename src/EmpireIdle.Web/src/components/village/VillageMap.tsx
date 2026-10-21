import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import { usePanZoom } from "../../hooks/usePanZoom";
import type { BuildingResponse } from "../../lib/apiTypes";
import { project, toPath, WORLD } from "../../lib/iso";
import { overlaps, snap, unproject, type PlanPoint } from "../../lib/villageLayout";
import type { Catalog } from "../../lib/queries/catalog";
import IsoBuilding from "./IsoBuilding";
import IsoWall from "./IsoWall";
import { buildingArt } from "./buildingArt";
import IsoBuildingOverlay from "./IsoBuildingOverlay";
import VillageTerrain from "./VillageTerrain";

/** Стіна — периметр, а не будівля на плані. Прапорця в конфізі немає, тож ключ. */
const WALL_KEY = "wall";

interface Props {
  buildings: BuildingResponse[];
  catalog: Catalog;
  selectedId: string | null;
  onSelect: (buildingId: string) => void;
  /** Режим «Перебудувати»: будівлі перетягуються мишею по сітці замість камери. */
  editing?: boolean;
  /** Будівлю відпущено на допустимому новому місці. */
  onMove?: (buildingId: string, x: number, y: number) => void;
}

/** Будівля, яку зараз тягнуть: де вона на сітці й чи можна її там поставити. */
interface Drag {
  id: string;
  from: PlanPoint;
  spot: PlanPoint;
  valid: boolean;
}

export default function VillageMap({ buildings, catalog, selectedId, onSelect, editing = false, onMove }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const { transform, handlers, wasDragged, moving } = usePanZoom(containerRef, WORLD);
  const [drag, setDrag] = useState<Drag | null>(null);
  const [hoveredId, setHoveredId] = useState<string | null>(null);

  const wall = buildings.find((building) => building.type === WALL_KEY) ?? null;
  const wallLocked = wall !== null && !wall.isUnlocked;
  const wallGateLevel = catalog.building(WALL_KEY)?.requiresMainBuildingLevel ?? 0;

  const placed = buildings
    .filter((building) => building.type !== WALL_KEY)
    .flatMap((building) => {
      const own = building.x != null && building.y != null ? { x: building.x, y: building.y } : null;
      const position = drag?.id === building.id ? drag.spot : (own ?? catalog.building(building.type)?.position);
      return position === null || position === undefined
        ? []
        : [{ building, position, art: buildingArt(building.type, building.level, position.x, position.y) }];
    })
    // Від дальніх до ближніх: ближня будівля має перекривати дальню
    .sort((a, b) => a.position.x + a.position.y - (b.position.x + b.position.y));

  const tap = (action: () => void) => () => {
    if (!wasDragged()) action();
  };

  /** Точка вказівника → вузол сітки плану з урахуванням зсуву й масштабу камери. */
  const spotAt = (clientX: number, clientY: number): PlanPoint | null => {
    const rect = containerRef.current?.getBoundingClientRect();
    if (rect === undefined || transform === null) return null;
    return snap(unproject((clientX - rect.left - transform.x) / transform.k, (clientY - rect.top - transform.y) / transform.k));
  };

  const fits = (id: string, spot: PlanPoint) =>
    placed.every(({ building, position }) => building.id === id || !overlaps(spot, position));

  // Поки тягнемо — слухаємо вікно: вказівник може вийти за межі будівлі чи мапи
  useEffect(() => {
    if (drag === null) return;

    const onMoveEvent = (event: PointerEvent) => {
      const spot = spotAt(event.clientX, event.clientY);
      if (spot !== null && (spot.x !== drag.spot.x || spot.y !== drag.spot.y))
        setDrag({ ...drag, spot, valid: fits(drag.id, spot) });
    };
    const onUp = () => {
      if (drag.valid && (drag.spot.x !== drag.from.x || drag.spot.y !== drag.from.y))
        onMove?.(drag.id, drag.spot.x, drag.spot.y);
      setDrag(null);
    };

    window.addEventListener("pointermove", onMoveEvent);
    window.addEventListener("pointerup", onUp);
    return () => {
      window.removeEventListener("pointermove", onMoveEvent);
      window.removeEventListener("pointerup", onUp);
    };
  });

  const startDrag = (id: string, position: PlanPoint) => (event: ReactPointerEvent) => {
    if (!editing) return;
    // Камера не панорамується, поки тягнемо будівлю
    event.stopPropagation();
    setDrag({ id, from: position, spot: position, valid: true });
  };

  /** Ромб основи будівлі на землі — зелений, якщо місце вільне, червоний — якщо ні. */
  const ghost =
    drag === null
      ? null
      : [
          project(drag.spot.x - 6, drag.spot.y - 6),
          project(drag.spot.x + 6, drag.spot.y - 6),
          project(drag.spot.x + 6, drag.spot.y + 6),
          project(drag.spot.x - 6, drag.spot.y + 6),
        ];

  // Подвір'я всередині муру — втоптана земля; усе за муром малює VillageTerrain
  const courtyard = [project(5, 5), project(95, 5), project(95, 95), project(5, 95)];

  return (
    <div
      ref={containerRef}
      {...handlers}
      className={`relative h-full w-full touch-none select-none overflow-hidden bg-gradient-to-b from-sky-200 to-sky-50 ${
        editing ? "ring-4 ring-inset ring-amber-400" : ""
      }`}
    >
      {transform !== null && (
        <svg className="absolute inset-0 h-full w-full">
          <g transform={`translate(${transform.x} ${transform.y}) scale(${transform.k})`}>
            <VillageTerrain part="ground" />
            <path d={toPath(courtyard)} fill="#e9e4d3" />
            {ghost !== null && (
              <path
                d={toPath(ghost)}
                fill={drag?.valid === true ? "rgba(16,185,129,0.35)" : "rgba(239,68,68,0.4)"}
                stroke={drag?.valid === true ? "#059669" : "#dc2626"}
                strokeWidth={2}
              />
            )}

            {wall !== null && (
              <IsoWall
                part="back"
                level={wall.level}
                name={catalog.buildingName(WALL_KEY)}
                selected={wall.id === selectedId}
                locked={wallLocked}
                gateLevel={wallGateLevel}
                onSelect={tap(() => onSelect(wall.id))}
              />
            )}

            {placed.map(({ building, position, art }) => (
              <g
                key={building.id}
                onPointerDown={startDrag(building.id, position)}
                onPointerEnter={() => setHoveredId(building.id)}
                onPointerLeave={() => setHoveredId((id) => (id === building.id ? null : id))}
                className={editing ? "cursor-grab" : undefined}
                opacity={drag?.id === building.id ? 0.75 : 1}
              >
                <IsoBuilding
                  art={art}
                  buildingType={building.type}
                  x={position.x}
                  y={position.y}
                  selected={building.id === selectedId}
                  underConstruction={building.isUnderConstruction}
                  locked={!building.isUnlocked}
                  onSelect={tap(() => onSelect(building.id))}
                />
              </g>
            ))}

            {wall !== null && (
              <IsoWall
                part="front"
                level={wall.level}
                name={catalog.buildingName(WALL_KEY)}
                selected={wall.id === selectedId}
                locked={wallLocked}
                gateLevel={wallGateLevel}
                onSelect={tap(() => onSelect(wall.id))}
              />
            )}

            <VillageTerrain part="front" />

            {/* Підписи поверх усього — ніщо їх не перекриває */}
            {placed.map(({ building, position, art }) => {
              const locked = !building.isUnlocked;
              const gateLevel = catalog.building(building.type)?.requiresMainBuildingLevel ?? 0;

              return (
                <IsoBuildingOverlay
                  key={`overlay-${building.id}`}
                  art={art}
                  x={position.x}
                  y={position.y}
                  name={catalog.buildingName(building.type)}
                  // Функціональна будівля рівнів не має (GDD §3.1) — лише назва
                  level={catalog.building(building.type)?.upgradable === false ? null : building.level}
                  gateLevel={locked ? gateLevel : null}
                  zoom={transform.k}
                  // Таблички — лише поки камера рухається або на будівлю навели; статичне село без написів
                  labelled={moving || building.id === hoveredId}
                  onSelect={tap(() => onSelect(building.id))}
                />
              );
            })}
          </g>
        </svg>
      )}
    </div>
  );
}
