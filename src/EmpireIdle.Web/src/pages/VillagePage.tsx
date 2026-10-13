import { useState } from "react";
import BuildingCard from "../components/BuildingCard";
import HungerBanner from "../components/HungerBanner";
import ErrorBanner from "../components/ErrorBanner";
import LockedBuildingCard from "../components/LockedBuildingCard";
import DamageBanner from "../components/village/DamageBanner";
import VillageMap from "../components/village/VillageMap";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import {
  useCollectAll,
  useCollectBuilding,
  useMoveBuilding,
  useRepairVillage,
  useSpeedUpBuilding,
  useUpgradeBuilding,
  useVillage,
} from "../lib/queries/village";

type VillageAction = "collect" | "upgrade" | "speedUp" | "collectAll" | "repair" | "move";

export default function VillagePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const village = useVillage(playerId);
  const collect = useCollectBuilding(playerId);
  const upgrade = useUpgradeBuilding(playerId);
  const speedUp = useSpeedUpBuilding(playerId);
  const collectAll = useCollectAll(playerId);
  const repair = useRepairVillage(playerId);
  const move = useMoveBuilding(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Режим «Перебудувати»: будівлі тягнуться мишею по сітці (рішення 13.10.2026)
  const [editing, setEditing] = useState(false);
  // Банер належить останній дії: помилка давнього кліку після успішного нового лише плутає
  const [lastAction, setLastAction] = useState<VillageAction | null>(null);

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження села…</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  const busy =
    collect.isPending || upgrade.isPending || speedUp.isPending || collectAll.isPending || repair.isPending;
  const actions = { collect, upgrade, speedUp, collectAll, repair, move };
  const failure = lastAction === null ? null : actions[lastAction].error;

  const run = (action: VillageAction, perform: () => void) => {
    setLastAction(action);
    perform();
  };

  const selected = village.data.buildings.find((building) => building.id === selectedId) ?? null;
  const collectable = village.data.buildings.some(
    (building) => building.isUnlocked && building.storedAmount > 0,
  );

  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">{village.data.name}</h1>
        <div className="flex-1" />
        <button
          type="button"
          onClick={() => {
            setEditing(!editing);
            setSelectedId(null);
          }}
          className={`rounded-lg px-3 py-1 text-sm font-medium ${
            editing ? "bg-amber-500 text-white hover:bg-amber-600" : "border border-slate-300 text-slate-700 hover:bg-slate-50"
          }`}
        >
          {editing ? "Готово" : "Перебудувати"}
        </button>
        <button
          type="button"
          onClick={() => run("collectAll", () => collectAll.mutate())}
          disabled={busy || !collectable}
          className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          Зібрати все
        </button>
      </div>

      <ErrorBanner error={failure} />

      {village.data.isHungry && <HungerBanner />}

      {village.data.damage != null && (
        <DamageBanner
          damage={village.data.damage}
          damagedNames={village.data.buildings
            .filter((building) => building.damageLevel > 0)
            .map((building) => catalog.buildingName(building.type))}
          resourceName={catalog.resourceName}
          busy={busy}
          onRepair={() => run("repair", () => repair.mutate())}
        />
      )}

      <div className="relative min-h-0 flex-1">
        <VillageMap
          buildings={village.data.buildings}
          catalog={catalog}
          selectedId={selectedId}
          onSelect={setSelectedId}
          onCollect={(buildingId) => run("collect", () => collect.mutate(buildingId))}
          editing={editing}
          onMove={(buildingId, x, y) => run("move", () => move.mutate({ buildingId, x, y }))}
        />

        {editing && (
          <p className="pointer-events-none absolute inset-x-0 top-3 mx-auto w-fit rounded-lg bg-amber-100/95 px-3 py-1 text-sm text-amber-900 shadow">
            Перетягніть будівлю на вільне місце всередині муру
          </p>
        )}

        {selected !== null && !editing && (
          <div className="absolute inset-x-3 bottom-3 max-w-sm">
            {selected.isUnlocked ? (
              <BuildingCard
                playerId={playerId}
                building={selected}
                busy={busy}
                onCollect={() => run("collect", () => collect.mutate(selected.id))}
                onUpgrade={() => run("upgrade", () => upgrade.mutate(selected.id))}
                onSpeedUp={() => run("speedUp", () => speedUp.mutate(selected.id))}
              />
            ) : (
              <LockedBuildingCard building={selected} />
            )}
          </div>
        )}
      </div>
    </div>
  );
}
