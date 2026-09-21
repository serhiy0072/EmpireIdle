import { useState } from "react";
import { useNow } from "../hooks/useNow";
import { useCatalog } from "../lib/queries/catalog";
import { useGarrison, useHealWounded, useRecoverUnits } from "../lib/queries/garrison";
import ErrorBanner from "./ErrorBanner";

interface Props {
  playerId: string;
}

/** Залишок часу до події за серверним часом. */
function remaining(at: string, now: number): string {
  const seconds = Math.max(0, Math.round((Date.parse(at) - now) / 1_000));

  const hours = Math.floor(seconds / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);
  const rest = seconds % 60;

  const pad = (value: number) => value.toString().padStart(2, "0");

  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}

/**
 * Госпіталь: лікування поранених (ресурсами або gems) і викуп юнітів,
 * що чекають своєї черги, за gems, поки не спливе дедлайн.
 */
export default function HospitalPanel({ playerId }: Props) {
  const now = useNow();
  const catalog = useCatalog();
  const garrison = useGarrison(playerId);
  const heal = useHealWounded(playerId);
  const recover = useRecoverUnits(playerId);

  const [healCounts, setHealCounts] = useState<Record<string, number>>({});
  const [recoverCounts, setRecoverCounts] = useState<Record<string, number>>({});

  const wounded = (garrison.data?.wounded ?? []).filter((stack) => stack.count > 0);
  const recoverable = (garrison.data?.recoverable ?? []).filter((stack) => stack.count > 0);

  if (wounded.length === 0 && recoverable.length === 0) {
    return null;
  }

  return (
    <div className="space-y-4 border-t border-slate-200 pt-3">
      <h4 className="text-sm font-medium text-slate-700">Госпіталь</h4>

      <ErrorBanner error={heal.error ?? recover.error} />

      {wounded.length > 0 && (
        <div className="space-y-3">
          <p className="text-xs font-medium text-slate-500">Поранені</p>
          {wounded.map((stack) => {
            const key = `${stack.unitType}@${stack.level}`;
            const unit = catalog.unit(stack.unitType);
            const count = Math.min(Math.max(healCounts[key] ?? 1, 1), stack.count);

            const resourceLabel = unit
              ? unit.cost
                  .map((line) => `${Math.ceil(line.amount * count * 0.5).toLocaleString("uk-UA")} ${catalog.resourceName(line.resource)}`)
                  .join(", ")
              : "";
            const gemsLabel = (count * catalog.healGemsPerUnit).toLocaleString("uk-UA");

            return (
              <div key={key} className="flex items-center justify-between gap-2 text-sm">
                <div>
                  <p className="text-slate-800">
                    {catalog.unitName(stack.unitType)} (рів. {stack.level}, поранено {stack.count})
                  </p>
                  <p className="text-xs text-slate-500">
                    {resourceLabel} або {gemsLabel} 💎
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <input
                    type="number"
                    min={1}
                    max={stack.count}
                    value={count}
                    onChange={(event) =>
                      setHealCounts((prev) => ({ ...prev, [key]: Math.max(1, Number(event.target.value)) }))
                    }
                    className="w-16 rounded-lg border border-slate-300 px-2 py-1 text-right"
                  />
                  <button
                    type="button"
                    onClick={() => heal.mutate({ units: { [key]: count }, payment: 1 })}
                    disabled={heal.isPending}
                    className="rounded-lg border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                  >
                    Ресурсами
                  </button>
                  <button
                    type="button"
                    onClick={() => heal.mutate({ units: { [key]: count }, payment: 2 })}
                    disabled={heal.isPending}
                    className="rounded-lg bg-emerald-600 px-3 py-1 text-white hover:bg-emerald-700 disabled:opacity-50"
                  >
                    Gems
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {recoverable.length > 0 && (
        <div className="space-y-3">
          <p className="text-xs font-medium text-slate-500">Чекають викупу</p>
          {recoverable.map((stack) => {
            const key = `${stack.unitType}@${stack.level}`;
            const count = Math.min(Math.max(recoverCounts[key] ?? 1, 1), stack.count);
            const gemsLabel = (count * stack.costGems).toLocaleString("uk-UA");

            return (
              <div key={key} className="flex items-center justify-between gap-2 text-sm">
                <div>
                  <p className="text-slate-800">
                    {catalog.unitName(stack.unitType)} (рів. {stack.level}, доступно {stack.count})
                  </p>
                  <p className="text-xs text-slate-500">
                    {gemsLabel} 💎 · згорить за {remaining(stack.expiresAt, now)}
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <input
                    type="number"
                    min={1}
                    max={stack.count}
                    value={count}
                    onChange={(event) =>
                      setRecoverCounts((prev) => ({ ...prev, [key]: Math.max(1, Number(event.target.value)) }))
                    }
                    className="w-16 rounded-lg border border-slate-300 px-2 py-1 text-right"
                  />
                  <button
                    type="button"
                    onClick={() => recover.mutate({ units: { [key]: count } })}
                    disabled={recover.isPending}
                    className="rounded-lg bg-emerald-600 px-3 py-1 text-white hover:bg-emerald-700 disabled:opacity-50"
                  >
                    Викупити
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
