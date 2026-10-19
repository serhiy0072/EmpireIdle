import { Link } from "react-router-dom";
import { useNow } from "../hooks/useNow";
import type { BuildingResponse } from "../lib/apiTypes";
import { screensFor } from "../lib/buildingScreens";
import { useCatalog } from "../lib/queries/catalog";
import { speedUpLabel } from "../lib/speedUp";
import { formatRemaining } from "../lib/time";
import HospitalPanel from "./HospitalPanel";
import SpeedUpItemsButton from "./SpeedUpItemsButton";
import TroopsPanel from "./TroopsPanel";

interface Props {
  playerId: string;
  building: BuildingResponse;
  busy: boolean;
  onUpgrade: () => void;
  onSpeedUp: () => void;
}

export default function BuildingCard({ playerId, building, busy, onUpgrade, onSpeedUp }: Props) {
  const now = useNow();

  const catalog = useCatalog();

  // Виробниче — збирається на склад само щохвилини (рішення 19.10.2026), кнопки «Зібрати» немає
  const resource = catalog.building(building.type)?.producesResource ?? null;
  // Функціональна будівля (ринок, кузня…) рівнів не має — ні підпису рівня, ні кнопки апгрейду (GDD §3.1)
  const upgradable = catalog.building(building.type)?.upgradable !== false;
  const screens = screensFor(building.type);

  // null — ціна невідома (показуємо просто дію), 0 — лишилась межа прискорення, кнопки немає
  const speedUpCost =
    building.constructionCompletesAt == null || building.speedUpCostGems == null
      ? null
      : catalog.speedUpCost("Construction", building.constructionCompletesAt, now, building.speedUpCostGems);

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
      <div className="flex items-baseline justify-between">
        <h3 className="font-medium text-slate-800">{catalog.buildingName(building.type)}</h3>
        {upgradable && <span className="text-sm text-slate-500">рів. {building.level}</span>}
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

      {resource !== null && (
        <p className="text-xs text-slate-500">
          Добуває {catalog.resourceName(resource).toLowerCase()} — усе вироблене щохвилини саме йде на склад.
        </p>
      )}

      {building.isUnderConstruction && building.constructionCompletesAt !== null && building.constructionCompletesAt !== undefined ? (
        <div className="flex items-center justify-between">
          <span className="text-sm text-slate-600">Будується: {formatRemaining(building.constructionCompletesAt, now)}</span>
          <span className="flex items-center gap-2">
            <SpeedUpItemsButton timer="Construction" targetId={building.id} completesAt={building.constructionCompletesAt} />
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
          </span>
        </div>
      ) : (
        <div className="flex gap-2">
          {upgradable && (
            <button
              type="button"
              data-tutorial="upgrade"
              onClick={onUpgrade}
              disabled={busy}
              className="flex-1 rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
            >
              Покращити
            </button>
          )}
        </div>
      )}

      <TroopsPanel playerId={playerId} buildingType={building.type} />

      {building.type === "hospital" && <HospitalPanel playerId={playerId} />}
    </div>
  );
}
