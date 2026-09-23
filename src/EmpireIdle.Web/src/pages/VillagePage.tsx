import { useState } from "react";
import BuildingCard from "../components/BuildingCard";
import ErrorBanner from "../components/ErrorBanner";
import LockedBuildingCard from "../components/LockedBuildingCard";
import VillageMap from "../components/village/VillageMap";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import {
  useCollectAll,
  useCollectBuilding,
  useSpeedUpBuilding,
  useUpgradeBuilding,
  useVillage,
} from "../lib/queries/village";

export default function VillagePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const village = useVillage(playerId);
  const collect = useCollectBuilding(playerId);
  const upgrade = useUpgradeBuilding(playerId);
  const speedUp = useSpeedUpBuilding(playerId);
  const collectAll = useCollectAll(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження села…</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  const busy = collect.isPending || upgrade.isPending || speedUp.isPending || collectAll.isPending;
  const failure = collect.error ?? upgrade.error ?? speedUp.error ?? collectAll.error;
  const selected = village.data.buildings.find((building) => building.id === selectedId) ?? null;
  const collectable = village.data.buildings.some(
    (building) => building.isUnlocked && !building.isUnderConstruction && building.storedAmount > 0,
  );

  return (
    <div className="flex h-[calc(100vh-9rem)] flex-col gap-3">
      <div className="flex items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">{village.data.name}</h1>
        <button
          type="button"
          onClick={() => collectAll.mutate()}
          disabled={busy || !collectable}
          className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          Зібрати все
        </button>
      </div>

      <ErrorBanner error={failure} />

      <div className="relative min-h-0 flex-1">
        <VillageMap
          buildings={village.data.buildings}
          catalog={catalog}
          selectedId={selectedId}
          onSelect={setSelectedId}
          onCollect={(buildingId) => collect.mutate(buildingId)}
        />

        {selected !== null && (
          <div className="absolute inset-x-3 bottom-3 max-w-sm">
            {selected.isUnlocked ? (
              <BuildingCard
                playerId={playerId}
                building={selected}
                busy={busy}
                onCollect={() => collect.mutate(selected.id)}
                onUpgrade={() => upgrade.mutate(selected.id)}
                onSpeedUp={() => speedUp.mutate(selected.id)}
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
