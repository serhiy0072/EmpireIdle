import { useState } from "react";
import { useNow } from "../hooks/useNow";
import { useCatalog } from "../lib/queries/catalog";
import { formatRemaining } from "../lib/time";
import { HEAL_PAYMENT, useGarrison, useHealWounded, useRecoverUnits } from "../lib/queries/garrison";
import ErrorBanner from "./ErrorBanner";
import { formatNumber } from "../lib/format";
import { t } from "i18next";

interface Props {
  playerId: string;
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

  // Порожній госпіталь теж видно: «Лазарет» на картці будівлі веде сюди, і гравець має побачити, що поранених немає
  if (wounded.length === 0 && recoverable.length === 0) {
    return (
      <div className="space-y-1 border-t border-slate-200 pt-3">
        <h4 className="text-sm font-medium text-slate-700">{t("village:hospital.title")}</h4>
        <p className="text-sm text-slate-500">{t("village:hospital.empty")}</p>
      </div>
    );
  }

  return (
    <div className="space-y-4 border-t border-slate-200 pt-3">
      <h4 className="text-sm font-medium text-slate-700">{t("village:hospital.title")}</h4>

      <ErrorBanner error={heal.error ?? recover.error} />

      {wounded.length > 0 && (
        <div className="space-y-3">
          <p className="text-xs font-medium text-slate-500">{t("village:hospital.wounded")}</p>
          {wounded.map((stack) => {
            const key = `${stack.unitType}@${stack.level}`;
            const unit = catalog.unit(stack.unitType);
            const count = Math.min(Math.max(healCounts[key] ?? 1, 1), stack.count);

            const resourceLabel = unit
              ? unit.cost
                  .map((line) => `${formatNumber(Math.ceil(line.amount * count * catalog.healResourceCostShare))} ${catalog.resourceName(line.resource)}`)
                  .join(", ")
              : "";
            const gemsLabel = formatNumber(count * catalog.healGemsPerUnit);

            return (
              <div key={key} className="flex items-center justify-between gap-2 text-sm">
                <div>
                  <p className="text-slate-800">
                    {t("village:hospital.woundedStack", { unit: catalog.unitName(stack.unitType), level: stack.level, count: stack.count })}
                  </p>
                  <p className="text-xs text-slate-500">
                    {t("village:hospital.cost", { resources: resourceLabel, gems: gemsLabel })}
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
                    onClick={() => heal.mutate({ units: { [key]: count }, payment: HEAL_PAYMENT.resources })}
                    disabled={heal.isPending}
                    className="rounded-lg border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                  >
                    {t("village:hospital.payResources")}
                  </button>
                  <button
                    type="button"
                    onClick={() => heal.mutate({ units: { [key]: count }, payment: HEAL_PAYMENT.gems })}
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
          <p className="text-xs font-medium text-slate-500">{t("village:hospital.recoverable")}</p>
          {recoverable.map((stack) => {
            const key = `${stack.unitType}@${stack.level}`;
            const count = Math.min(Math.max(recoverCounts[key] ?? 1, 1), stack.count);
            const gemsLabel = formatNumber(count * stack.costGems);

            return (
              <div key={key} className="flex items-center justify-between gap-2 text-sm">
                <div>
                  <p className="text-slate-800">
                    {t("village:hospital.recoverableStack", { unit: catalog.unitName(stack.unitType), level: stack.level, count: stack.count })}
                  </p>
                  <p className="text-xs text-slate-500">
                    {t("village:hospital.expires", { gems: gemsLabel, remaining: formatRemaining(stack.expiresAt, now) })}
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
                    {t("village:hospital.ransom")}
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
