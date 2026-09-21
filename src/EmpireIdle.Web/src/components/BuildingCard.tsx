import { useNow } from "../hooks/useNow";
import type { BuildingResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import TrainUnitsPanel from "./TrainUnitsPanel";

interface Props {
  playerId: string;
  building: BuildingResponse;
  busy: boolean;
  onCollect: () => void;
  onUpgrade: () => void;
  onSpeedUp: () => void;
}

/** Залишок до кінця будівництва за серверним часом: годинник клієнта в розрахунку не бере участі. */
function remaining(completesAt: string, now: number): string {
  const seconds = Math.max(0, Math.round((Date.parse(completesAt) - now) / 1_000));

  const hours = Math.floor(seconds / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);
  const rest = seconds % 60;

  const pad = (value: number) => value.toString().padStart(2, "0");

  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}

export default function BuildingCard({ playerId, building, busy, onCollect, onUpgrade, onSpeedUp }: Props) {
  const now = useNow();

  const fill = building.storageCap > 0 ? Math.min(1, building.storedAmount / building.storageCap) : 0;
  const full = building.storageCap > 0 && building.storedAmount >= building.storageCap;
  const catalog = useCatalog();

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
      <div className="flex items-baseline justify-between">
        <h3 className="font-medium text-slate-800">{catalog.buildingName(building.type)}</h3>
        <span className="text-sm text-slate-500">рів. {building.level}</span>
      </div>

      <div>
        <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
          <div
            className={`h-full ${full ? "bg-amber-500" : "bg-emerald-500"}`}
            style={{ width: `${fill * 100}%` }}
          />
        </div>
        <p className="mt-1 text-xs text-slate-500">
          {building.storedAmount.toLocaleString("uk-UA")} / {building.storageCap.toLocaleString("uk-UA")}
          {full && " — сховище заповнене, виробіток стоїть"}
        </p>
      </div>

      {building.isUnderConstruction && building.constructionCompletesAt !== null && building.constructionCompletesAt !== undefined ? (
        <div className="flex items-center justify-between">
          <span className="text-sm text-slate-600">Будується: {remaining(building.constructionCompletesAt, now)}</span>
          <button
            type="button"
            onClick={onSpeedUp}
            disabled={busy}
            className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            {building.speedUpCostGems === null || building.speedUpCostGems === undefined
              ? "Прискорити"
              : building.speedUpCostGems === 0
                ? "Прискорити (безкоштовно)"
                : `Прискорити (${building.speedUpCostGems.toLocaleString("uk-UA")} 💎)`}
          </button>
        </div>
      ) : (
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onCollect}
            disabled={busy || building.storedAmount === 0}
            className="flex-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Зібрати
          </button>
          <button
            type="button"
            onClick={onUpgrade}
            disabled={busy}
            className="flex-1 rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            Покращити
          </button>
        </div>
      )}

      <TrainUnitsPanel playerId={playerId} buildingType={building.type} buildingLevel={building.level} />
    </div>
  );
}
