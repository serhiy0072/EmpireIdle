import { useState } from "react";
import { useNow } from "../hooks/useNow";
import { cumulativeUnitLevelCost } from "../lib/progression";
import { useCatalog } from "../lib/queries/catalog";
import { useGarrison, useLevelUpUnits, useSpeedUpLevelUp } from "../lib/queries/garrison";
import ErrorBanner from "./ErrorBanner";

interface Props {
  playerId: string;
  buildingType: string;
}

/** Залишок до кінця прокачки за серверним часом. */
function remaining(completesAt: string, now: number): string {
  const seconds = Math.max(0, Math.round((Date.parse(completesAt) - now) / 1_000));

  const hours = Math.floor(seconds / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);
  const rest = seconds % 60;

  const pad = (value: number) => value.toString().padStart(2, "0");

  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}

/**
 * Прокачка вже навчених юнітів на вищий рівень (§5.2 GDD): +10%/рівень,
 * кап 10. Юніти на прокачці зняті з гарнізону, поки не завершиться.
 */
export default function LevelUpUnitsPanel({ playerId, buildingType }: Props) {
  const now = useNow();
  const catalog = useCatalog();
  const garrison = useGarrison(playerId);
  const levelUp = useLevelUpUnits(playerId);
  const speedUp = useSpeedUpLevelUp(playerId);

  const [targetLevels, setTargetLevels] = useState<Record<string, number>>({});
  const [counts, setCounts] = useState<Record<string, number>>({});

  const belongsToBuilding = (unitType: string) => catalog.unit(unitType)?.requiresBuilding === buildingType;

  const stacks = (garrison.data?.units ?? []).filter(
    (stack) => stack.count > 0 && stack.level < catalog.maxUnitLevel && belongsToBuilding(stack.unitType),
  );

  const queue = (garrison.data?.levelUpOrders ?? []).filter((order) => belongsToBuilding(order.unitType));

  if (stacks.length === 0 && queue.length === 0) {
    return null;
  }

  return (
    <div className="space-y-3 border-t border-slate-200 pt-3">
      <h4 className="text-sm font-medium text-slate-700">Прокачка</h4>

      <ErrorBanner error={levelUp.error ?? speedUp.error} />

      {stacks.map((stack) => {
        const key = `${stack.unitType}@${stack.level}`;
        const unit = catalog.unit(stack.unitType);
        const maxToLevel = catalog.maxUnitLevel;
        const toLevel = Math.min(Math.max(targetLevels[key] ?? stack.level + 1, stack.level + 1), maxToLevel);
        const count = Math.min(Math.max(counts[key] ?? 1, 1), stack.count);

        const costLine = unit
          ? unit.cost
              .map((line) => {
                const perUnit = cumulativeUnitLevelCost(line.amount, stack.level, toLevel, unit.levelUpCostGrowth);
                return `${(perUnit * count).toLocaleString("uk-UA")} ${catalog.resourceName(line.resource)}`;
              })
              .join(", ")
          : "";

        const minutes = unit
          ? cumulativeUnitLevelCost(unit.baseTrainMinutes, stack.level, toLevel, unit.levelUpCostGrowth) * count
          : 0;

        const levelOptions: number[] = [];
        for (let lvl = stack.level + 1; lvl <= maxToLevel; lvl++) levelOptions.push(lvl);

        return (
          <div key={key} className="flex items-center justify-between gap-2 text-sm">
            <div>
              <p className="text-slate-800">
                {catalog.unitName(stack.unitType)} (рів. {stack.level}, є {stack.count})
              </p>
              <p className="text-xs text-slate-500">
                {costLine} · {minutes} хв
              </p>
            </div>

            <div className="flex items-center gap-2">
              <select
                value={toLevel}
                onChange={(event) =>
                  setTargetLevels((prev) => ({ ...prev, [key]: Number(event.target.value) }))
                }
                className="rounded-lg border border-slate-300 px-1 py-1 text-sm"
              >
                {levelOptions.map((lvl) => (
                  <option key={lvl} value={lvl}>
                    до {lvl}
                  </option>
                ))}
              </select>
              <input
                type="number"
                min={1}
                max={stack.count}
                value={count}
                onChange={(event) =>
                  setCounts((prev) => ({ ...prev, [key]: Math.max(1, Number(event.target.value)) }))
                }
                className="w-16 rounded-lg border border-slate-300 px-2 py-1 text-right"
              />
              <button
                type="button"
                onClick={() => levelUp.mutate({ unitType: stack.unitType, fromLevel: stack.level, toLevel, count })}
                disabled={levelUp.isPending}
                className="rounded-lg bg-emerald-600 px-3 py-1 text-white hover:bg-emerald-700 disabled:opacity-50"
              >
                Прокачати
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
                {catalog.unitName(order.unitType)} ×{order.count} (рів. {order.fromLevel}→{order.toLevel}) —{" "}
                {remaining(order.completesAt, now)}
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
