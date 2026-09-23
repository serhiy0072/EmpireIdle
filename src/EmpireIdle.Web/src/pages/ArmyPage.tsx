import ErrorBanner from "../components/ErrorBanner";
import HospitalPanel from "../components/HospitalPanel";
import LevelUpUnitsPanel from "../components/LevelUpUnitsPanel";
import TrainUnitsPanel from "../components/TrainUnitsPanel";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import { useGarrison } from "../lib/queries/garrison";
import { useVillage } from "../lib/queries/village";

/**
 * Військо: увесь гарнізон в одному місці, без переходу через конкретну
 * будівлю на мапі села. Тренування й прокачка тут — ті самі панелі,
 * що на картці будівлі: одна дія, два входи.
 */
export default function ArmyPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
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

  // Будівлі, що тренують: список іде з довідника, а не зашитий у сторінку — стайня з'явиться сама
  const trainingBuildings = village.data.buildings.filter(
    (building) => building.isUnlocked && catalog.trainingBuildingKeys.includes(building.type),
  );

  const units = garrison.data.units.filter((unit) => unit.count > 0);
  const total = units.reduce((sum, unit) => sum + unit.count, 0);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Військо</h1>
        <p className="text-sm text-slate-500">Разом у гарнізоні: {total.toLocaleString("uk-UA")}</p>
      </div>

      <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-medium text-slate-700">Гарнізон</h2>

        {units.length === 0 ? (
          <p className="text-sm text-slate-500">Армія порожня — навчіть перших юнітів у казармах.</p>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2">
            {units.map((unit) => (
              <div
                key={`${unit.unitType}@${unit.level}`}
                className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm"
              >
                <span className="text-slate-800">
                  {catalog.unitName(unit.unitType)} (рів. {unit.level})
                </span>
                <span className="font-medium text-slate-700">{unit.count.toLocaleString("uk-UA")}</span>
              </div>
            ))}
          </div>
        )}

        {trainingBuildings.map((building) => (
          <div key={building.id}>
            <TrainUnitsPanel
              playerId={playerId}
              buildingType={building.type}
              buildingLevel={building.level}
              underConstruction={building.isUnderConstruction}
            />
            <LevelUpUnitsPanel playerId={playerId} buildingType={building.type} />
          </div>
        ))}

        <HospitalPanel playerId={playerId} />
      </div>
    </div>
  );
}
