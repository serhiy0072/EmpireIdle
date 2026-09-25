import { Link } from "react-router-dom";
import { useNow } from "../hooks/useNow";
import type { BuildingResponse } from "../lib/apiTypes";
import { screensFor } from "../lib/buildingScreens";
import { useCatalog } from "../lib/queries/catalog";
import { speedUpLabel } from "../lib/speedUp";
import { formatRemaining } from "../lib/time";
import HospitalPanel from "./HospitalPanel";
import LevelUpUnitsPanel from "./LevelUpUnitsPanel";
import TrainUnitsPanel from "./TrainUnitsPanel";

interface Props {
  playerId: string;
  building: BuildingResponse;
  busy: boolean;
  onCollect: () => void;
  onUpgrade: () => void;
  onSpeedUp: () => void;
}

export default function BuildingCard({ playerId, building, busy, onCollect, onUpgrade, onSpeedUp }: Props) {
  const now = useNow();

  const catalog = useCatalog();

  // Сховище й збір є лише в будівель, що щось виробляють: ратуші чи казармам бар "0 / 0" ні до чого
  const produces = catalog.building(building.type)?.producesResource != null;
  const fill = building.storageCap > 0 ? Math.min(1, building.storedAmount / building.storageCap) : 0;
  const full = building.storageCap > 0 && building.storedAmount >= building.storageCap;
  const screens = screensFor(building.type);

  // null — ціна невідома (показуємо просто дію), 0 — лишилась межа прискорення, кнопки немає
  const speedUpCost =
    building.constructionCompletesAt == null || building.speedUpCostGems == null
      ? null
      : catalog.speedUpCost(building.constructionCompletesAt, now, building.speedUpCostGems);

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
      <div className="flex items-baseline justify-between">
        <h3 className="font-medium text-slate-800">{catalog.buildingName(building.type)}</h3>
        <span className="text-sm text-slate-500">рів. {building.level}</span>
      </div>

      {building.damageLevel > 0 && building.damagedUntil != null && (
        <p className="rounded-lg bg-rose-50 px-3 py-1.5 text-sm text-rose-800">
          Пошкоджено (рівень {building.damageLevel}) — темп удвічі нижчий. Відновиться за{" "}
          {formatRemaining(building.damagedUntil, now)}.
        </p>
      )}

      {/* Будівля веде на свій екран: казарма — до війська, зала героїв — до героїв */}
      {screens.map((screen) => (
        <Link
          key={screen.to}
          to={screen.to}
          className="flex items-center justify-between rounded-lg bg-slate-800 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-900"
        >
          <span>{screen.label}</span>
          <span aria-hidden>→</span>
        </Link>
      ))}

      {produces && (
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
      )}

      {building.isUnderConstruction && building.constructionCompletesAt !== null && building.constructionCompletesAt !== undefined ? (
        <div className="flex items-center justify-between">
          <span className="text-sm text-slate-600">Будується: {formatRemaining(building.constructionCompletesAt, now)}</span>
          {speedUpCost !== 0 && (
            <button
              type="button"
              onClick={onSpeedUp}
              disabled={busy}
              className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
            >
              {speedUpLabel(speedUpCost)}
            </button>
          )}
        </div>
      ) : (
        <div className="flex gap-2">
          {produces && (
            <button
              type="button"
              data-tutorial="collect"
              onClick={onCollect}
              disabled={busy || building.storedAmount === 0}
              className="flex-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              Зібрати
            </button>
          )}
          <button
            type="button"
            data-tutorial="upgrade"
            onClick={onUpgrade}
            disabled={busy}
            className="flex-1 rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            Покращити
          </button>
        </div>
      )}

      <TrainUnitsPanel
        playerId={playerId}
        buildingType={building.type}
        buildingLevel={building.level}
        underConstruction={building.isUnderConstruction}
      />
      <LevelUpUnitsPanel playerId={playerId} buildingType={building.type} />
      {building.type === "hospital" && <HospitalPanel playerId={playerId} />}
    </div>
  );
}
