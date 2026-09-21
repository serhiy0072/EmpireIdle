import { useRef } from "react";
import { usePanZoom } from "../../hooks/usePanZoom";
import type { BuildingResponse } from "../../lib/apiTypes";
import { project, toPath, WORLD } from "../../lib/iso";
import type { Catalog } from "../../lib/queries/catalog";
import IsoBuilding from "./IsoBuilding";
import IsoWall from "./IsoWall";

/** Стіна — периметр, а не будівля на плані. Прапорця в конфізі немає, тож ключ. */
const WALL_KEY = "wall";

function compact(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return value.toString();
}

interface Props {
  buildings: BuildingResponse[];
  catalog: Catalog;
  selectedId: string | null;
  onSelect: (buildingId: string) => void;
  onCollect: (buildingId: string) => void;
}

export default function VillageMap({ buildings, catalog, selectedId, onSelect, onCollect }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const { transform, handlers, wasDragged } = usePanZoom(containerRef, WORLD);

  const wall = buildings.find((building) => building.type === WALL_KEY) ?? null;

  const placed = buildings
    .filter((building) => building.type !== WALL_KEY)
    .flatMap((building) => {
      const position = catalog.building(building.type)?.position;
      return position === null || position === undefined ? [] : [{ building, position }];
    })
    // Від дальніх до ближніх: ближня будівля має перекривати дальню
    .sort((a, b) => a.position.x + a.position.y - (b.position.x + b.position.y));

  const tap = (action: () => void) => () => {
    if (!wasDragged()) action();
  };

  const ground = [project(0, 0), project(100, 0), project(100, 100), project(0, 100)];
  const courtyard = [project(5, 5), project(95, 5), project(95, 95), project(5, 95)];

  return (
    <div
      ref={containerRef}
      {...handlers}
      className="relative h-full w-full touch-none select-none overflow-hidden rounded-xl bg-gradient-to-b from-sky-200 to-sky-50"
    >
      {transform !== null && (
        <svg className="absolute inset-0 h-full w-full">
          <g transform={`translate(${transform.x} ${transform.y}) scale(${transform.k})`}>
            <path d={toPath(ground)} fill="#dbeafe" />
            <path d={toPath(courtyard)} fill="#f1f5f9" />

            {wall !== null && (
              <IsoWall
                part="back"
                level={wall.level}
                name={catalog.buildingName(WALL_KEY)}
                selected={wall.id === selectedId}
                onSelect={tap(() => onSelect(wall.id))}
              />
            )}

            {placed.map(({ building, position }) => {
              const collectable =
                !building.isUnderConstruction && building.storageCap > 0 && building.storedAmount > 0;

              return (
                <IsoBuilding
                  key={building.id}
                  buildingKey={building.type}
                  name={catalog.buildingName(building.type)}
                  x={position.x}
                  y={position.y}
                  level={building.level}
                  selected={building.id === selectedId}
                  underConstruction={building.isUnderConstruction}
                  bubble={collectable ? compact(building.storedAmount) : null}
                  onSelect={tap(() => onSelect(building.id))}
                  onCollect={tap(() => onCollect(building.id))}
                />
              );
            })}

            {wall !== null && (
              <IsoWall
                part="front"
                level={wall.level}
                name={catalog.buildingName(WALL_KEY)}
                selected={wall.id === selectedId}
                onSelect={tap(() => onSelect(wall.id))}
              />
            )}
          </g>
        </svg>
      )}
    </div>
  );
}
