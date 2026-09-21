import { useState } from "react";
import { useNow } from "../hooks/useNow";
import { cumulativeUnitLevelCost } from "../lib/progression";
import type { CatalogUnit } from "../lib/queries/catalog";
import { useCatalog } from "../lib/queries/catalog";
import { useGarrison, useSpeedUpTraining, useTrainUnits } from "../lib/queries/garrison";
import ErrorBanner from "./ErrorBanner";

interface Props {
  playerId: string;
  buildingType: string;
  buildingLevel: number;
}

/** Залишок до кінця тренування за серверним часом. */
function remaining(completesAt: string, now: number): string {
  const seconds = Math.max(0, Math.round((Date.parse(completesAt) - now) / 1_000));

  const hours = Math.floor(seconds / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);
  const rest = seconds % 60;

  const pad = (value: number) => value.toString().padStart(2, "0");

  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}

/**
 * Тренування з нуля на обраний рівень коштує суму кроків від 1 до нього —
 * так само, як прокачка вже навченого юніта (§5.2 GDD).
 */
function costLabel(unit: CatalogUnit, level: number, count: number, resourceName: (key: string) => string): string {
  return unit.cost
    .map((line) => {
      const perUnit = cumulativeUnitLevelCost(line.amount, 1, level + 1, unit.levelUpCostGrowth);
      return `${(perUnit * count).toLocaleString("uk-UA")} ${resourceName(line.resource)}`;
    })
    .join(", ");
}

/** Тренування юнітів на конкретній будівлі (казарми, стайня…) — список доступних типів і черга. */
export default function TrainUnitsPanel({ playerId, buildingType, buildingLevel }: Props) {
  const now = useNow();
  const catalog = useCatalog();
  const garrison = useGarrison(playerId);
  const train = useTrainUnits(playerId);
  const speedUp = useSpeedUpTraining(playerId);
  const [counts, setCounts] = useState<Record<string, number>>({});
  const [levels, setLevels] = useState<Record<string, number>>({});

  const units = catalog.unitsFor(buildingType, buildingLevel);
  const unitKeys = new Set(units.map((unit) => unit.key));
  const queue = (garrison.data?.trainingOrders ?? []).filter((order) => unitKeys.has(order.unitType));

  if (units.length === 0) {
    return null;
  }

  return (
    <div className="space-y-3 border-t border-slate-200 pt-3">
      <h4 className="text-sm font-medium text-slate-700">Тренування</h4>

      <ErrorBanner error={train.error ?? speedUp.error} />

      {units.map((unit) => {
        const count = counts[unit.key] ?? 1;
        const level = levels[unit.key] ?? 1;
        const minutes = cumulativeUnitLevelCost(unit.baseTrainMinutes, 1, level + 1, unit.levelUpCostGrowth) * count;

        return (
          <div key={unit.key} className="flex items-center justify-between gap-2 text-sm">
            <div>
              <p className="text-slate-800">{unit.displayName}</p>
              <p className="text-xs text-slate-500">
                {costLabel(unit, level, count, catalog.resourceName)} · {minutes} хв
              </p>
            </div>

            <div className="flex items-center gap-2">
              <select
                value={level}
                onChange={(event) =>
                  setLevels((prev) => ({ ...prev, [unit.key]: Number(event.target.value) }))
                }
                className="rounded-lg border border-slate-300 px-1 py-1 text-sm"
              >
                {Array.from({ length: catalog.maxUnitLevel }, (_, index) => index + 1).map((lvl) => (
                  <option key={lvl} value={lvl}>
                    рів. {lvl}
                  </option>
                ))}
              </select>
              <input
                type="number"
                min={1}
                value={count}
                onChange={(event) =>
                  setCounts((prev) => ({ ...prev, [unit.key]: Math.max(1, Number(event.target.value)) }))
                }
                className="w-16 rounded-lg border border-slate-300 px-2 py-1 text-right"
              />
              <button
                type="button"
                onClick={() => train.mutate({ unitType: unit.key, level, count })}
                disabled={train.isPending}
                className="rounded-lg bg-emerald-600 px-3 py-1 text-white hover:bg-emerald-700 disabled:opacity-50"
              >
                Тренувати
              </button>
            </div>
          </div>
        );
      })}

      {queue.length > 0 && (
        <div className="space-y-1 border-t border-slate-100 pt-2">
          <p className="text-xs font-medium text-slate-500">У черзі</p>
          {queue.map((order) => (
            <div key={order.id} className="flex items-center justify-between text-sm text-slate-600">
              <span>
                {catalog.unitName(order.unitType)} ×{order.count} (рів. {order.level}) — {remaining(order.completesAt, now)}
              </span>
              <button
                type="button"
                onClick={() => speedUp.mutate(order.id)}
                disabled={speedUp.isPending}
                className="rounded-lg border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                {order.speedUpCostGems === 0
                  ? "Прискорити (безкоштовно)"
                  : `Прискорити (${order.speedUpCostGems.toLocaleString("uk-UA")} 💎)`}
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
