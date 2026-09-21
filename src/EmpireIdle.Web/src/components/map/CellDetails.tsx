import type { MapCellDetailsResponse, MarchTargetType } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_TARGET } from "../../lib/queries/marches";
import { terrainLabel } from "../../lib/terrain";

interface Props {
  cell: MapCellDetailsResponse;
  isHome: boolean;
  onAttack: (target: { type: MarchTargetType; id: string; name: string }) => void;
}

/** Обрана клітина: місцевість і хто на ній стоїть. Кнопка нападу — лише на чуже. */
export default function CellDetails({ cell, isHome, onAttack }: Props) {
  const catalog = useCatalog();

  const occupant = cell.occupantType ?? null;
  const name = cell.occupantName ?? (occupant === "Monster" ? "Монстр" : occupant === "Village" ? "Село" : "");
  const targetType: MarchTargetType | null =
    occupant === "Monster" ? MARCH_TARGET.monster : occupant === "Village" ? MARCH_TARGET.village : null;

  return (
    <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-medium text-slate-800">
          {isHome ? "Ваше село" : occupant === null ? terrainLabel(cell.terrainType) : name}
        </h3>
        <span className="text-xs text-slate-500">
          ({cell.x}, {cell.y})
        </span>
      </div>

      <p className="text-sm text-slate-600">
        {terrainLabel(cell.terrainType)} · хід ×{cell.moveCost}
        {!cell.passable && " · непрохідна"}
      </p>

      {occupant === "Monster" && (
        <div className="space-y-1 text-sm">
          <p className="text-slate-700">Рівень {cell.monsterLevel ?? "?"}</p>
          {cell.monsterUnits !== null && cell.monsterUnits !== undefined && (
            <ul className="text-slate-600">
              {Object.entries(cell.monsterUnits).map(([unitType, count]) => (
                <li key={unitType}>
                  {catalog.unitName(unitType)} × {count}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {targetType !== null && !isHome && cell.occupantId !== null && cell.occupantId !== undefined && (
        <button
          type="button"
          onClick={() => onAttack({ type: targetType, id: cell.occupantId as string, name })}
          className="w-full rounded-lg bg-rose-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-rose-700"
        >
          Атакувати
        </button>
      )}
    </div>
  );
}
