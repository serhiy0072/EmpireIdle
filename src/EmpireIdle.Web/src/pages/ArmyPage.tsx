import { Link } from "react-router-dom";
import { UnitFigure } from "../components/army/UnitArt";
import ErrorBanner from "../components/ErrorBanner";
import HospitalPanel from "../components/HospitalPanel";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import { useGarrison } from "../lib/queries/garrison";
import { useVillage } from "../lib/queries/village";
import { formatRemaining } from "../lib/time";
import { roman } from "../lib/unitTiers";
import { formatNumber } from "../lib/format";

/**
 * Військо: увесь гарнізон в одному місці. Тренування й прокачка живуть у таборах — по одному
 * на будівлю (референс 13.10.2026); тут — вхід у кожен табір зі станом його черги й лазарет.
 */
export default function ArmyPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();
  const catalog = useCatalog();
  const village = useVillage(playerId);
  const garrison = useGarrison(playerId);

  if (garrison.isPending || village.isPending) {
    return <p className="text-slate-500">Завантаження війська…</p>;
  }

  if (garrison.isError) {
    return <ErrorBanner error={garrison.error} />;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  // Будівлі, що тренують: список іде з довідника, а не зашитий у сторінку — нова будівля з'явиться сама
  // Порядок — як у вкладках табору
  const camps = catalog.trainingBuildingKeys
    .map((key) => village.data.buildings.find((building) => building.type === key && building.isUnlocked))
    .filter((building) => building !== undefined);

  const units = garrison.data.units.filter((unit) => unit.count > 0);
  const total = units.reduce((sum, unit) => sum + unit.count, 0);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Військо</h1>
        <p className="text-sm text-slate-500">Разом у гарнізоні: {formatNumber(total)}</p>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        {camps.map((camp) => {
          const unit = catalog.unitsFor(camp.type, camp.level)[0];
          const keys = new Set(catalog.unitsFor(camp.type, camp.level).map((candidate) => candidate.key));
          const training = garrison.data.trainingOrders.find((order) => keys.has(order.unitType));
          const owned = units.filter((stack) => keys.has(stack.unitType)).reduce((sum, stack) => sum + stack.count, 0);

          return (
            <Link
              key={camp.id}
              to={`/army/${camp.type}`}
              data-tutorial={`camp:${camp.type}`}
              className="flex items-center gap-3 rounded-xl bg-gradient-to-b from-[#3b6aa8] to-[#1d3b6f] p-3 text-white shadow hover:brightness-110"
            >
              {unit !== undefined && <UnitFigure unitType={unit.key} level={1} size={48} />}
              <span className="min-w-0 flex-1">
                <span className="block font-semibold">{catalog.buildingName(camp.type)}</span>
                <span className="block text-xs text-sky-100">
                  {unit?.displayName ?? "—"}: {formatNumber(owned)}
                </span>
                <span className="block text-xs text-lime-300">
                  {training === undefined
                    ? "Черга вільна"
                    : `${roman(training.level)} ×${training.count} — ${formatRemaining(training.completesAt, now)}`}
                </span>
              </span>
            </Link>
          );
        })}
      </div>

      <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-medium text-slate-700">Гарнізон</h2>

        {units.length === 0 ? (
          <p className="text-sm text-slate-500">Армія порожня — навчіть перших юнітів у таборах.</p>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2">
            {units.map((unit) => (
              <div
                key={`${unit.unitType}@${unit.level}`}
                className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm"
              >
                <span className="text-slate-800">
                  {catalog.unitName(unit.unitType)} {roman(unit.level)}
                </span>
                <span className="font-medium text-slate-700">{formatNumber(unit.count)}</span>
              </div>
            ))}
          </div>
        )}

        <HospitalPanel playerId={playerId} />
      </div>
    </div>
  );
}
